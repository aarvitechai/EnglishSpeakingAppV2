using SpeakMate.Domain.Common;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Learners;

namespace SpeakMate.Domain.Practice;

/// <summary>
/// Aggregate root for one practice conversation. Owns its turns and its feedback, and
/// enforces the conversation rules: the coach opens, speakers alternate, ended sessions are read-only.
/// </summary>
public sealed class PracticeSession : Entity
{
    public const int TopicMaxLength = 120;

    /// <summary>Hard cap on learner messages per session; bounds AI cost and context size.</summary>
    public const int MaxLearnerTurns = 30;

    private readonly List<ConversationTurn> _turns = [];

    private PracticeSession() { } // EF Core

    private PracticeSession(Guid userId, PracticeMode mode, EnglishLevel level, string topic,
        bool saveTranscript, DateTimeOffset now)
    {
        UserId = Guard.NotEmpty(userId, "session.user_required");
        if (!Enum.IsDefined(mode))
        {
            throw new DomainException("session.mode_invalid", "Unknown practice mode.");
        }

        if (!Enum.IsDefined(level))
        {
            throw new DomainException("session.level_invalid", "Unknown English level.");
        }

        Mode = mode;
        Level = level;
        Topic = Guard.NotBlank(topic, "session.topic_required", "Topic is required.", TopicMaxLength);
        SaveTranscript = saveTranscript;
        Status = SessionStatus.Active;
        StartedAt = now;
        LastActivityAt = now;
    }

    public Guid UserId { get; private set; }
    public PracticeMode Mode { get; private set; }

    /// <summary>Learner level captured at session start, so later profile changes do not rewrite history.</summary>
    public EnglishLevel Level { get; private set; }
    public string Topic { get; private set; } = string.Empty;

    /// <summary>Whether the learner consented to keep the transcript after the session ends.</summary>
    public bool SaveTranscript { get; private set; }
    public SessionStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public bool TranscriptCleared { get; private set; }

    /// <summary>Concurrency token: stops two simultaneous requests from corrupting a conversation.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<ConversationTurn> Turns => _turns;
    public SessionFeedback? Feedback { get; private set; }

    /// <summary>Learner messages sent. Persisted, so statistics survive when a transcript is cleared.</summary>
    public int LearnerMessageCount { get; private set; }

    public bool IsActive => Status == SessionStatus.Active;
    public bool HasReachedTurnLimit => LearnerMessageCount >= MaxLearnerTurns;
    public TimeSpan Duration => (EndedAt ?? LastActivityAt) - StartedAt;

    public static PracticeSession Start(Guid userId, PracticeMode mode, EnglishLevel level, string topic,
        bool saveTranscript, DateTimeOffset now) =>
        new(userId, mode, level, topic, saveTranscript, now);

    public ConversationTurn AddCoachTurn(string text, DateTimeOffset now)
    {
        EnsureActive();
        if (_turns.Count > 0 && _turns[^1].Speaker == Speaker.Coach)
        {
            throw new DomainException("session.turn_order", "The coach already replied; waiting for the learner.");
        }

        return Append(Speaker.Coach, text, InputMethod.Text, speechConfidence: null, now);
    }

    public ConversationTurn AddLearnerTurn(string text, InputMethod inputMethod, double? speechConfidence,
        DateTimeOffset now)
    {
        EnsureActive();
        if (_turns.Count == 0 || _turns[^1].Speaker != Speaker.Coach)
        {
            throw new DomainException("session.turn_order", "Wait for the coach to reply before sending another message.");
        }

        if (HasReachedTurnLimit)
        {
            throw new DomainException("session.turn_limit",
                $"This session reached its limit of {MaxLearnerTurns} messages. Finish it to get feedback.");
        }

        if (!Enum.IsDefined(inputMethod))
        {
            throw new DomainException("turn.input_method_invalid", "Unknown input method.");
        }

        if (speechConfidence is < 0 or > 1)
        {
            throw new DomainException("turn.confidence_invalid", "Speech confidence must be between 0 and 1.");
        }

        var turn = Append(Speaker.Learner, text, inputMethod, speechConfidence, now);
        LearnerMessageCount++;
        return turn;
    }

    /// <summary>Ends the session normally. Feedback can be attached afterwards.</summary>
    public void Complete(DateTimeOffset now)
    {
        EnsureActive();
        if (LearnerMessageCount == 0)
        {
            throw new DomainException("session.empty", "Send at least one message before finishing the session.");
        }

        Status = SessionStatus.Completed;
        EndedAt = now;
        LastActivityAt = now;
    }

    /// <summary>Ends the session without feedback (the learner left, or it expired).</summary>
    public void Abandon(DateTimeOffset now)
    {
        EnsureActive();
        Status = SessionStatus.Abandoned;
        EndedAt = now;
    }

    /// <summary>True when an active session has had no activity for longer than <paramref name="idleLimit"/>.</summary>
    public bool IsIdle(DateTimeOffset now, TimeSpan idleLimit) => IsActive && now - LastActivityAt > idleLimit;

    public void AttachFeedback(SessionFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        if (Status != SessionStatus.Completed)
        {
            throw new DomainException("session.not_completed", "Feedback can only be added to a completed session.");
        }

        if (Feedback is not null)
        {
            throw new DomainException("session.feedback_exists", "This session already has feedback.");
        }

        if (feedback.SessionId != Id)
        {
            throw new DomainException("session.feedback_mismatch", "Feedback belongs to a different session.");
        }

        Feedback = feedback;
    }

    /// <summary>
    /// Removes the conversation text when the learner did not consent to keep it.
    /// Called after feedback is generated, so the learner still gets their feedback.
    /// </summary>
    public bool ClearTranscriptIfNotConsented()
    {
        if (IsActive || SaveTranscript || TranscriptCleared)
        {
            return false;
        }

        _turns.Clear();
        TranscriptCleared = true;
        return true;
    }

    private ConversationTurn Append(Speaker speaker, string text, InputMethod inputMethod,
        double? speechConfidence, DateTimeOffset now)
    {
        var turn = new ConversationTurn(Id, _turns.Count + 1, speaker, text, inputMethod, speechConfidence, now);
        _turns.Add(turn);
        LastActivityAt = now;
        return turn;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException("session.not_active", "This practice session has already ended.");
        }
    }
}
