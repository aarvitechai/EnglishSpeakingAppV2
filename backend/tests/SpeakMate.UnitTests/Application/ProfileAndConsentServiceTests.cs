using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;
using SpeakMate.Application.Consents;
using SpeakMate.Application.Profiles;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Learners;

namespace SpeakMate.UnitTests.Application;

public sealed class ProfileServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly ILearnerProfileRepository _profiles = Substitute.For<ILearnerProfileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero));
    private readonly ProfileService _service;

    public ProfileServiceTests() => _service = new ProfileService(_profiles, _unitOfWork, _clock);

    private static UpdatePreferencesRequest Prefs() =>
        new(EnglishLevel.Advanced, [LearningGoal.Study], "es", 30);

    [Fact]
    public async Task Get_WhenProfileMissing_ReturnsNotFound()
    {
        var result = await _service.GetAsync(UserId, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task CompleteOnboarding_UpdatesProfileAndSaves()
    {
        var profile = LearnerProfile.Create(UserId, "Lena", _clock.GetUtcNow());
        _profiles.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _service.CompleteOnboardingAsync(UserId, Prefs(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OnboardingCompleted);
        Assert.Equal(_clock.GetUtcNow(), profile.OnboardingCompletedAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenProfileMissing_DoesNotSave()
    {
        var result = await _service.UpdatePreferencesAsync(UserId, Prefs(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public sealed class ConsentServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly IConsentRepository _consents = Substitute.For<IConsentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero));
    private readonly ConsentService _service;

    public ConsentServiceTests()
    {
        _service = new ConsentService(_consents, _unitOfWork, _clock);
        _consents.ListForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact]
    public async Task GetStatus_ListsEveryConsentType()
    {
        var status = await _service.GetStatusAsync(UserId, TestContext.Current.CancellationToken);

        Assert.Equal(Enum.GetValues<ConsentType>().Length, status.Count);
        Assert.All(status, s => Assert.False(s.Granted));
        Assert.True(status.Single(s => s.Type == ConsentType.TermsAndPrivacy).Required);
    }

    [Fact]
    public async Task Grant_AddsRecordWithCurrentPolicyVersion()
    {
        var result = await _service.SetAsync(UserId, ConsentType.TranscriptStorage, granted: true,
            TestContext.Current.CancellationToken);

        Assert.True(result.Value.Granted);
        _consents.Received(1).Add(Arg.Is<ConsentRecord>(r =>
            r.Type == ConsentType.TranscriptStorage && r.PolicyVersion == Policies.CurrentVersion));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Grant_WhenAlreadyGranted_IsIdempotent()
    {
        var existing = ConsentRecord.Grant(UserId, ConsentType.TranscriptStorage, "2026-11", _clock.GetUtcNow());
        _consents.ListForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([existing]);

        var result = await _service.SetAsync(UserId, ConsentType.TranscriptStorage, granted: true,
            TestContext.Current.CancellationToken);

        Assert.True(result.Value.Granted);
        _consents.DidNotReceive().Add(Arg.Any<ConsentRecord>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Withdraw_MarksLatestRecordWithdrawn()
    {
        var existing = ConsentRecord.Grant(UserId, ConsentType.ProductImprovement, "2026-11", _clock.GetUtcNow());
        _consents.ListForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([existing]);
        _clock.Advance(TimeSpan.FromDays(1));

        var result = await _service.SetAsync(UserId, ConsentType.ProductImprovement, granted: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Value.Granted);
        Assert.Equal(_clock.GetUtcNow(), existing.WithdrawnAt);
    }

    [Fact]
    public async Task WithdrawTerms_ReturnsValidationError()
    {
        var result = await _service.SetAsync(UserId, ConsentType.TermsAndPrivacy, granted: false,
            TestContext.Current.CancellationToken);

        Assert.Equal("consent.required", result.Error!.Code);
    }

    [Fact]
    public async Task HasActiveConsent_ReflectsLatestDecision()
    {
        var existing = ConsentRecord.Grant(UserId, ConsentType.TranscriptStorage, "2026-11", _clock.GetUtcNow());
        _consents.ListForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([existing]);

        Assert.True(await _service.HasActiveConsentAsync(UserId, ConsentType.TranscriptStorage,
            TestContext.Current.CancellationToken));

        existing.Withdraw(_clock.GetUtcNow());
        Assert.False(await _service.HasActiveConsentAsync(UserId, ConsentType.TranscriptStorage,
            TestContext.Current.CancellationToken));
    }
}
