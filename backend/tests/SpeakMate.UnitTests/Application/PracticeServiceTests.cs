using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;
using SpeakMate.Application.Practice;
using SpeakMate.Application.Usage;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;
using SpeakMate.Domain.Usage;

namespace SpeakMate.UnitTests.Application;

public sealed class PracticeServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly AiUsage Usage = new("gemini-test", 1_000_000, 1_000_000, 250);

    private readonly IPracticeSessionRepository _sessions = Substitute.For<IPracticeSessionRepository>();
    private readonly ILearnerProfileRepository _profiles = Substitute.For<ILearnerProfileRepository>();
    private readonly IConsentRepository _consents = Substitute.For<IConsentRepository>();
    private readonly IUsageEventRepository _usageEvents = Substitute.For<IUsageEventRepository>();
    private readonly IUsageRecorder _usageRecorder = Substitute.For<IUsageRecorder>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAiCoach _coach = Substitute.For<IAiCoach>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero));
    private readonly UsageLimits _limits = new()
    {
        DailyTokenLimitPerUser = 10_000_000,
        MaxActiveSessionsPerUser = 2,
        Pricing = { ["gemini-test"] = new ModelPrice { InputPerMillionUsd = 0.30m, OutputPerMillionUsd = 2.50m } },
    };

    private readonly PracticeService _service;
    private readonly LearnerProfile _profile;

    public PracticeServiceTests()
    {
        _service = new PracticeService(_sessions, _profiles, _consents, _usageEvents, _usageRecorder, _unitOfWork, _coach,
            new UsageGuard(_usageEvents, _limits, _clock), _limits, _clock, NullLogger<PracticeService>.Instance);

        _profile = LearnerProfile.Create(UserId, "Kim", _clock.GetUtcNow());
        _profile.CompleteOnboarding(EnglishLevel.Intermediate, [LearningGoal.Work], "en", 10, _clock.GetUtcNow());
        _profiles.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(_profile);
        _consents.ListForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([]);
        _coach.GenerateReplyAsync(Arg.Any<CoachContext>(), Arg.Any<CancellationToken>())
            .Returns(new CoachReply("Hello! How are you today?", Usage));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_CreatesSessionWithCoachOpeningAndRecordsCost()
    {
        var result = await _service.StartAsync(UserId, new StartSessionRequest(PracticeMode.DailyConversation, "Hobbies"), Ct);

        Assert.True(result.IsSuccess);
        var turn = Assert.Single(result.Value.Turns);
        Assert.Equal(Speaker.Coach, turn.Speaker);
        Assert.Equal(EnglishLevel.Intermediate, result.Value.Level);
        Assert.Equal("Hobbies", result.Value.Topic);
        _sessions.Received(1).Add(Arg.Any<PracticeSession>());
        _usageEvents.Received(1).Add(Arg.Is<UsageEvent>(u => u.Succeeded && u.EstimatedCostUsd == 2.80m));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WithoutTopic_PicksOneFromCatalog()
    {
        var result = await _service.StartAsync(UserId, new StartSessionRequest(PracticeMode.RolePlay, null), Ct);

        Assert.Contains(result.Value.Topic, TopicCatalog.For(PracticeMode.RolePlay));
    }

    [Fact]
    public async Task Start_UsesTranscriptConsent()
    {
        _consents.ListForUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns([ConsentRecord.Grant(UserId, ConsentType.TranscriptStorage, "2026-11", _clock.GetUtcNow())]);

        var result = await _service.StartAsync(UserId, new StartSessionRequest(PracticeMode.RolePlay, "Shop"), Ct);

        Assert.True(result.Value.SaveTranscript);
    }

    [Fact]
    public async Task Start_BeforeOnboarding_IsRejected()
    {
        _profiles.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(LearnerProfile.Create(UserId, "New", _clock.GetUtcNow()));

        var result = await _service.StartAsync(UserId, new StartSessionRequest(PracticeMode.RolePlay, "Shop"), Ct);

        Assert.Equal("session.onboarding_required", result.Error!.Code);
        await _coach.DidNotReceive().GenerateReplyAsync(Arg.Any<CoachContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WithTooManyActiveSessions_IsConflict()
    {
        _sessions.CountActiveForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(2);

        var result = await _service.StartAsync(UserId, new StartSessionRequest(PracticeMode.RolePlay, "Shop"), Ct);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Start_OverDailyQuota_IsRejectedBeforeCallingAi()
    {
        _usageEvents.SumTokensForUserSinceAsync(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_limits.DailyTokenLimitPerUser);

        var result = await _service.StartAsync(UserId, new StartSessionRequest(PracticeMode.RolePlay, "Shop"), Ct);

        Assert.Equal(ErrorType.TooManyRequests, result.Error!.Type);
        await _coach.DidNotReceive().GenerateReplyAsync(Arg.Any<CoachContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenAiFails_SavesNothingButRecordsUsageInIsolation()
    {
        _coach.GenerateReplyAsync(Arg.Any<CoachContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AiProviderException("blocked", Usage));

        var result = await _service.StartAsync(UserId, new StartSessionRequest(PracticeMode.RolePlay, "Shop"), Ct);

        Assert.Equal("ai.unavailable", result.Error!.Code);
        _sessions.DidNotReceive().Add(Arg.Any<PracticeSession>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _usageRecorder.Received(1).SaveIsolatedAsync(Arg.Is<UsageEvent>(u => !u.Succeeded), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_AddsLearnerAndCoachTurns()
    {
        var session = PracticeSession.Start(UserId, PracticeMode.DailyConversation, EnglishLevel.Intermediate, "Food",
            false, _clock.GetUtcNow());
        session.AddCoachTurn("What do you like to eat?", _clock.GetUtcNow());
        _sessions.GetForUserAsync(session.Id, UserId, Arg.Any<CancellationToken>()).Returns(session);
        _coach.GenerateReplyAsync(Arg.Any<CoachContext>(), Arg.Any<CancellationToken>())
            .Returns(new CoachReply("Yum! Do you cook it yourself?", Usage));

        var result = await _service.SendMessageAsync(UserId, session.Id,
            new SendMessageRequest("I like pasta.", InputMethod.Voice, 0.88), Ct);

        Assert.Equal([Speaker.Coach, Speaker.Learner, Speaker.Coach], result.Value.Turns.Select(t => t.Speaker));
        await _coach.Received(1).GenerateReplyAsync(
            Arg.Is<CoachContext>(c => c.Turns.Count == 2 && c.Turns[1].SpeechConfidence == 0.88),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_ToOtherUsersSession_IsNotFound()
    {
        var result = await _service.SendMessageAsync(UserId, Guid.NewGuid(),
            new SendMessageRequest("Hi", InputMethod.Text, null), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task SendMessage_ToEndedSession_IsConflict()
    {
        var session = PracticeSession.Start(UserId, PracticeMode.DailyConversation, EnglishLevel.Beginner, "Food",
            false, _clock.GetUtcNow());
        session.Abandon(_clock.GetUtcNow());
        _sessions.GetForUserAsync(session.Id, UserId, Arg.Any<CancellationToken>()).Returns(session);

        var result = await _service.SendMessageAsync(UserId, session.Id,
            new SendMessageRequest("Hi", InputMethod.Text, null), Ct);

        Assert.Equal("session.not_active", result.Error!.Code);
    }

    [Fact]
    public async Task SendMessage_WhenAiFails_DoesNotSaveLearnerTurn()
    {
        var session = PracticeSession.Start(UserId, PracticeMode.DailyConversation, EnglishLevel.Beginner, "Food",
            false, _clock.GetUtcNow());
        session.AddCoachTurn("Hi!", _clock.GetUtcNow());
        _sessions.GetForUserAsync(session.Id, UserId, Arg.Any<CancellationToken>()).Returns(session);
        _coach.GenerateReplyAsync(Arg.Any<CoachContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AiProviderException("timeout"));

        var result = await _service.SendMessageAsync(UserId, session.Id,
            new SendMessageRequest("Hello", InputMethod.Text, null), Ct);

        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _usageRecorder.DidNotReceive().SaveIsolatedAsync(Arg.Any<UsageEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void UsageGuard_UnknownModel_CostsZero()
    {
        var guard = new UsageGuard(_usageEvents, _limits, _clock);

        Assert.Equal(0m, guard.EstimateCostUsd(new AiUsage("unknown", 1000, 1000, 1)));
        Assert.Equal(0.00028m, guard.EstimateCostUsd(new AiUsage("gemini-test", 100, 100, 1)));
    }
}
