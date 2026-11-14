using SpeakMate.Domain.Common;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;

namespace SpeakMate.UnitTests.Domain;

public sealed class PracticeSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private static PracticeSession NewSession(bool saveTranscript = true) =>
        PracticeSession.Start(UserId, PracticeMode.DailyConversation, EnglishLevel.Beginner, "Weekend plans",
            saveTranscript, Now);

    private static PracticeSession SessionWithOneExchange(bool saveTranscript = true)
    {
        var session = NewSession(saveTranscript);
        session.AddCoachTurn("Hi! What did you do last weekend?", Now);
        session.AddLearnerTurn("I go to the park.", InputMethod.Voice, 0.82, Now.AddSeconds(20));
        return session;
    }

    [Fact]
    public void Start_CreatesActiveSessionWithTrimmedTopic()
    {
        var session = PracticeSession.Start(UserId, PracticeMode.RolePlay, EnglishLevel.Advanced, "  Ordering food  ",
            saveTranscript: false, Now);

        Assert.True(session.IsActive);
        Assert.Equal("Ordering food", session.Topic);
        Assert.Equal(EnglishLevel.Advanced, session.Level);
        Assert.Equal(Now, session.StartedAt);
        Assert.Empty(session.Turns);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_WithBlankTopic_Throws(string topic)
    {
        var ex = Assert.Throws<DomainException>(() =>
            PracticeSession.Start(UserId, PracticeMode.RolePlay, EnglishLevel.Beginner, topic, true, Now));

        Assert.Equal("session.topic_required", ex.Code);
    }

    [Fact]
    public void Start_WithEmptyUser_Throws() =>
        Assert.Throws<DomainException>(() =>
            PracticeSession.Start(Guid.Empty, PracticeMode.RolePlay, EnglishLevel.Beginner, "Topic", true, Now));

    [Fact]
    public void Start_WithUndefinedMode_Throws()
    {
        var ex = Assert.Throws<DomainException>(() =>
            PracticeSession.Start(UserId, (PracticeMode)99, EnglishLevel.Beginner, "Topic", true, Now));

        Assert.Equal("session.mode_invalid", ex.Code);
    }

    [Fact]
    public void Turns_AlternateAndAreNumberedInOrder()
    {
        var session = SessionWithOneExchange();
        session.AddCoachTurn("Nice! Which park?", Now.AddSeconds(25));

        Assert.Equal([1, 2, 3], session.Turns.Select(t => t.Sequence));
        Assert.Equal([Speaker.Coach, Speaker.Learner, Speaker.Coach], session.Turns.Select(t => t.Speaker));
        Assert.Equal(Now.AddSeconds(25), session.LastActivityAt);
    }

    [Fact]
    public void AddLearnerTurn_BeforeCoachOpens_Throws()
    {
        var session = NewSession();

        var ex = Assert.Throws<DomainException>(() =>
            session.AddLearnerTurn("Hello", InputMethod.Text, null, Now));

        Assert.Equal("session.turn_order", ex.Code);
    }

    [Fact]
    public void AddLearnerTurn_TwiceInARow_Throws()
    {
        var session = SessionWithOneExchange();

        var ex = Assert.Throws<DomainException>(() =>
            session.AddLearnerTurn("Again", InputMethod.Text, null, Now));

        Assert.Equal("session.turn_order", ex.Code);
    }

    [Fact]
    public void AddCoachTurn_TwiceInARow_Throws()
    {
        var session = NewSession();
        session.AddCoachTurn("Hello!", Now);

        Assert.Throws<DomainException>(() => session.AddCoachTurn("Hello again!", Now));
    }

    [Fact]
    public void AddLearnerTurn_TooLong_Throws()
    {
        var session = NewSession();
        session.AddCoachTurn("Hi!", Now);

        Assert.Throws<DomainException>(() => session.AddLearnerTurn(
            new string('a', ConversationTurn.MaxTextLength + 1), InputMethod.Text, null, Now));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void AddLearnerTurn_WithOutOfRangeConfidence_Throws(double confidence)
    {
        var session = NewSession();
        session.AddCoachTurn("Hi!", Now);

        var ex = Assert.Throws<DomainException>(() =>
            session.AddLearnerTurn("Hello", InputMethod.Voice, confidence, Now));

        Assert.Equal("turn.confidence_invalid", ex.Code);
    }

    [Fact]
    public void AddLearnerTurn_AfterTurnLimit_Throws()
    {
        var session = NewSession();
        for (var i = 0; i < PracticeSession.MaxLearnerTurns; i++)
        {
            session.AddCoachTurn($"Question {i}", Now);
            session.AddLearnerTurn($"Answer {i}", InputMethod.Text, null, Now);
        }

        session.AddCoachTurn("One more?", Now);

        Assert.True(session.HasReachedTurnLimit);
        var ex = Assert.Throws<DomainException>(() =>
            session.AddLearnerTurn("Extra", InputMethod.Text, null, Now));
        Assert.Equal("session.turn_limit", ex.Code);
    }

    [Fact]
    public void Complete_SetsStatusAndDuration()
    {
        var session = SessionWithOneExchange();

        session.Complete(Now.AddMinutes(5));

        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal(TimeSpan.FromMinutes(5), session.Duration);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void Complete_WithoutAnyLearnerMessage_Throws()
    {
        var session = NewSession();
        session.AddCoachTurn("Hi!", Now);

        var ex = Assert.Throws<DomainException>(() => session.Complete(Now));

        Assert.Equal("session.empty", ex.Code);
    }

    [Fact]
    public void EndedSession_RejectsNewTurns()
    {
        var session = SessionWithOneExchange();
        session.Complete(Now);

        var ex = Assert.Throws<DomainException>(() => session.AddCoachTurn("Still there?", Now));

        Assert.Equal("session.not_active", ex.Code);
    }

    [Fact]
    public void Abandon_EndsSessionEvenWithoutMessages()
    {
        var session = NewSession();

        session.Abandon(Now);

        Assert.Equal(SessionStatus.Abandoned, session.Status);
        Assert.Throws<DomainException>(() => session.Abandon(Now));
    }

    [Fact]
    public void AttachFeedback_OnCompletedSession_Succeeds()
    {
        var session = SessionWithOneExchange();
        session.Complete(Now);
        var feedback = SessionFeedback.Create(session.Id, "Good effort.", [], ["I went to the park."],
            "Describe your weekend again using past tense.", Now);

        session.AttachFeedback(feedback);

        Assert.Same(feedback, session.Feedback);
    }

    [Fact]
    public void AttachFeedback_OnActiveSession_Throws()
    {
        var session = SessionWithOneExchange();
        var feedback = SessionFeedback.Create(session.Id, "Summary", [], [], "Task", Now);

        var ex = Assert.Throws<DomainException>(() => session.AttachFeedback(feedback));

        Assert.Equal("session.not_completed", ex.Code);
    }

    [Fact]
    public void AttachFeedback_Twice_Throws()
    {
        var session = SessionWithOneExchange();
        session.Complete(Now);
        session.AttachFeedback(SessionFeedback.Create(session.Id, "Summary", [], [], "Task", Now));

        var ex = Assert.Throws<DomainException>(() =>
            session.AttachFeedback(SessionFeedback.Create(session.Id, "Summary", [], [], "Task", Now)));

        Assert.Equal("session.feedback_exists", ex.Code);
    }

    [Fact]
    public void AttachFeedback_ForAnotherSession_Throws()
    {
        var session = SessionWithOneExchange();
        session.Complete(Now);

        var ex = Assert.Throws<DomainException>(() =>
            session.AttachFeedback(SessionFeedback.Create(Guid.NewGuid(), "Summary", [], [], "Task", Now)));

        Assert.Equal("session.feedback_mismatch", ex.Code);
    }

    [Fact]
    public void ClearTranscript_WithoutConsent_RemovesTurnsAfterSessionEnds()
    {
        var session = SessionWithOneExchange(saveTranscript: false);
        session.Complete(Now);

        var cleared = session.ClearTranscriptIfNotConsented();

        Assert.True(cleared);
        Assert.Empty(session.Turns);
        Assert.True(session.TranscriptCleared);
    }

    [Fact]
    public void ClearTranscript_WithConsent_KeepsTurns()
    {
        var session = SessionWithOneExchange(saveTranscript: true);
        session.Complete(Now);

        Assert.False(session.ClearTranscriptIfNotConsented());
        Assert.Equal(2, session.Turns.Count);
    }

    [Fact]
    public void ClearTranscript_WhileActive_DoesNothing()
    {
        var session = SessionWithOneExchange(saveTranscript: false);

        Assert.False(session.ClearTranscriptIfNotConsented());
        Assert.Equal(2, session.Turns.Count);
    }
}
