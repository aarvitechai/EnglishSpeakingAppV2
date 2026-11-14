using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;
using SpeakMate.Domain.Usage;
using SpeakMate.Infrastructure.Persistence.Repositories;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Persistence;

/// <summary>Verifies the EF Core mapping against real SQL Server: round-trips, JSON columns, concurrency.</summary>
public sealed class PersistenceTests(TestDatabase database) : IntegrationTestBase(database)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.FromHours(5.5));
    private Guid _userId;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await Database.CreateUserAsync();
    }

    private async Task<PracticeSession> SaveCompletedSessionAsync(bool saveTranscript = true)
    {
        await using var db = Database.CreateDbContext();
        var session = PracticeSession.Start(_userId, PracticeMode.InterviewPractice, EnglishLevel.Intermediate,
            "Job interview", saveTranscript, Now);
        session.AddCoachTurn("Tell me about yourself.", Now);
        session.AddLearnerTurn("I am working as developer since 2020.", InputMethod.Voice, 0.91, Now.AddSeconds(30));
        session.Complete(Now.AddMinutes(4));
        session.AttachFeedback(SessionFeedback.Create(session.Id, "Clear answer with good structure.",
            [new Correction(CorrectionCategory.Grammar, "I am working as developer since 2020",
                "I have been working as a developer since 2020", "Use the present perfect continuous with since.")],
            ["In my current role, I..."], "Answer again using: I have been...", Now.AddMinutes(4)));
        db.PracticeSessions.Add(session);
        await db.SaveChangesAsync(Ct);
        return session;
    }

    [Fact]
    public async Task PracticeSession_RoundTripsWithTurnsAndFeedback()
    {
        var saved = await SaveCompletedSessionAsync();

        await using var db = Database.CreateDbContext();
        var loaded = await new PracticeSessionRepository(db).GetForUserAsync(saved.Id, _userId, Ct);

        Assert.NotNull(loaded);
        Assert.Equal(SessionStatus.Completed, loaded.Status);
        Assert.Equal(Now, loaded.StartedAt);
        Assert.Equal([Speaker.Coach, Speaker.Learner], loaded.Turns.Select(t => t.Speaker));
        Assert.Equal(0.91, loaded.Turns[1].SpeechConfidence);
        Assert.NotNull(loaded.Feedback);
        var correction = Assert.Single(loaded.Feedback.Corrections);
        Assert.Equal(CorrectionCategory.Grammar, correction.Category);
        Assert.Equal("I have been working as a developer since 2020", correction.Improved);
        Assert.Equal(["In my current role, I..."], loaded.Feedback.UsefulPhrases);
        Assert.NotEmpty(loaded.RowVersion);
    }

    [Fact]
    public async Task GetForUser_WithAnotherUsersId_ReturnsNull()
    {
        var saved = await SaveCompletedSessionAsync();

        await using var db = Database.CreateDbContext();
        var loaded = await new PracticeSessionRepository(db).GetForUserAsync(saved.Id, Guid.NewGuid(), Ct);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ClearingTranscript_DeletesTurnRowsFromDatabase()
    {
        var saved = await SaveCompletedSessionAsync(saveTranscript: false);

        await using (var db = Database.CreateDbContext())
        {
            var session = await new PracticeSessionRepository(db).GetForUserAsync(saved.Id, _userId, Ct);
            Assert.True(session!.ClearTranscriptIfNotConsented());
            await db.SaveChangesAsync(Ct);
        }

        await using var verify = Database.CreateDbContext();
        Assert.Equal(0, await verify.ConversationTurns.CountAsync(t => t.SessionId == saved.Id, Ct));
        Assert.True(await verify.PracticeSessions.Where(s => s.Id == saved.Id).Select(s => s.TranscriptCleared)
            .SingleAsync(Ct));
    }

    [Fact]
    public async Task ConcurrentUpdatesToSameSession_SecondSaveThrowsConcurrencyConflict()
    {
        Guid sessionId;
        await using (var db = Database.CreateDbContext())
        {
            var session = PracticeSession.Start(_userId, PracticeMode.DailyConversation, EnglishLevel.Beginner,
                "Hobbies", true, Now);
            session.AddCoachTurn("What do you like to do?", Now);
            db.PracticeSessions.Add(session);
            await db.SaveChangesAsync(Ct);
            sessionId = session.Id;
        }

        // Two requests load the same session and both reply.
        await using var first = Database.CreateDbContext();
        await using var second = Database.CreateDbContext();
        var a = await new PracticeSessionRepository(first).GetForUserAsync(sessionId, _userId, Ct);
        var b = await new PracticeSessionRepository(second).GetForUserAsync(sessionId, _userId, Ct);
        a!.AddLearnerTurn("I like reading.", InputMethod.Text, null, Now.AddSeconds(5));
        b!.AddLearnerTurn("I like football.", InputMethod.Text, null, Now.AddSeconds(6));

        await ((IUnitOfWork)first).SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => ((IUnitOfWork)second).SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task LearnerProfile_RoundTripsGoalsAndEnforcesOneProfilePerUser()
    {
        await using (var db = Database.CreateDbContext())
        {
            var profile = LearnerProfile.Create(_userId, "Ravi", Now);
            profile.CompleteOnboarding(EnglishLevel.Advanced, [LearningGoal.Work, LearningGoal.Confidence], "hi", 20, Now);
            db.LearnerProfiles.Add(profile);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = Database.CreateDbContext())
        {
            var loaded = await new LearnerProfileRepository(db).GetByUserIdAsync(_userId, Ct);
            Assert.Equal([LearningGoal.Work, LearningGoal.Confidence], loaded!.Goals);
            Assert.True(loaded.HasCompletedOnboarding);
        }

        await using var duplicate = Database.CreateDbContext();
        duplicate.LearnerProfiles.Add(LearnerProfile.Create(_userId, "Ravi again", Now));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task UsageEvents_SumTokensOnlyWithinWindowAndForUser()
    {
        var otherUserId = await Database.CreateUserAsync();
        await using (var db = Database.CreateDbContext())
        {
            db.UsageEvents.AddRange(
                UsageEvent.Record(_userId, null, AiOperation.ConversationReply, "test-model", 100, 50, 0.001m, true, 300, Now),
                UsageEvent.Record(_userId, null, AiOperation.SessionFeedback, "test-model", 200, 100, 0.002m, true, 400, Now.AddHours(1)),
                UsageEvent.Record(_userId, null, AiOperation.ConversationReply, "test-model", 999, 999, 0.01m, true, 300, Now.AddDays(-2)),
                UsageEvent.Record(otherUserId, null, AiOperation.ConversationReply, "test-model", 500, 500, 0.01m, true, 300, Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var read = Database.CreateDbContext();
        var total = await new UsageEventRepository(read).SumTokensForUserSinceAsync(_userId, Now.AddHours(-1), Ct);

        Assert.Equal(450, total);
    }

    [Fact]
    public async Task ConsentRecords_ListInGrantOrder()
    {
        await using (var db = Database.CreateDbContext())
        {
            db.ConsentRecords.AddRange(
                ConsentRecord.Grant(_userId, ConsentType.TranscriptStorage, "2026-11", Now.AddMinutes(1)),
                ConsentRecord.Grant(_userId, ConsentType.TermsAndPrivacy, "2026-11", Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var read = Database.CreateDbContext();
        var records = await new ConsentRepository(read).ListForUserAsync(_userId, Ct);

        Assert.Equal([ConsentType.TermsAndPrivacy, ConsentType.TranscriptStorage], records.Select(r => r.Type));
    }

    [Fact]
    public async Task DeletingUser_CascadesToAllLearnerData()
    {
        await SaveCompletedSessionAsync();
        await using (var db = Database.CreateDbContext())
        {
            db.LearnerProfiles.Add(LearnerProfile.Create(_userId, "Temp", Now));
            db.ConsentRecords.Add(ConsentRecord.Grant(_userId, ConsentType.TermsAndPrivacy, "2026-11", Now));
            db.UsageEvents.Add(UsageEvent.Record(_userId, null, AiOperation.ConversationReply, "m", 1, 1, 0, true, 1, Now));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = Database.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == _userId).ExecuteDeleteAsync(Ct);
        }

        await using var verify = Database.CreateDbContext();
        Assert.False(await verify.PracticeSessions.AnyAsync(Ct));
        Assert.False(await verify.ConversationTurns.AnyAsync(Ct));
        Assert.False(await verify.SessionFeedback.AnyAsync(Ct));
        Assert.False(await verify.LearnerProfiles.AnyAsync(Ct));
        Assert.False(await verify.ConsentRecords.AnyAsync(Ct));
        Assert.False(await verify.UsageEvents.AnyAsync(Ct));
    }
}
