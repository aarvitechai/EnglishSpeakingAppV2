using SpeakMate.Domain.Common;

namespace SpeakMate.Domain.Feedback;

/// <summary>
/// End-of-session coaching feedback. Deliberately small: at most
/// <see cref="MaxCorrections"/> corrections so learners are not overwhelmed.
/// </summary>
public sealed class SessionFeedback : Entity
{
    public const int MaxCorrections = 3;
    public const int MaxUsefulPhrases = 5;
    public const int SummaryMaxLength = 1000;
    public const int RepeatTaskMaxLength = 500;
    public const int PhraseMaxLength = 200;

    private readonly List<Correction> _corrections = [];
    private readonly List<string> _usefulPhrases = [];

    private SessionFeedback() { } // EF Core

    private SessionFeedback(Guid sessionId, string summary, IEnumerable<Correction> corrections,
        IEnumerable<string> usefulPhrases, string repeatTask, DateTimeOffset now)
    {
        SessionId = Guard.NotEmpty(sessionId, "feedback.session_required");
        Summary = Guard.NotBlank(summary, "feedback.summary_required", "Summary is required.", SummaryMaxLength);
        RepeatTask = Guard.NotBlank(repeatTask, "feedback.repeat_task_required",
            "A repeat task is required.", RepeatTaskMaxLength);

        _corrections = corrections.ToList();
        if (_corrections.Count > MaxCorrections)
        {
            throw new DomainException("feedback.too_many_corrections",
                $"Feedback may contain at most {MaxCorrections} corrections.");
        }

        _usefulPhrases = usefulPhrases
            .Select(p => Guard.NotBlank(p, "feedback.phrase_invalid", "Phrases cannot be empty.", PhraseMaxLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (_usefulPhrases.Count > MaxUsefulPhrases)
        {
            throw new DomainException("feedback.too_many_phrases",
                $"Feedback may contain at most {MaxUsefulPhrases} useful phrases.");
        }

        CreatedAt = now;
    }

    public Guid SessionId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public IReadOnlyList<Correction> Corrections => _corrections;
    public IReadOnlyList<string> UsefulPhrases => _usefulPhrases;
    public string RepeatTask { get; private set; } = string.Empty;

    /// <summary>The learner's own rating of whether this feedback helped (null until they answer).</summary>
    public bool? WasHelpful { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SessionFeedback Create(Guid sessionId, string summary, IEnumerable<Correction> corrections,
        IEnumerable<string> usefulPhrases, string repeatTask, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(corrections);
        ArgumentNullException.ThrowIfNull(usefulPhrases);
        return new SessionFeedback(sessionId, summary, corrections, usefulPhrases, repeatTask, now);
    }

    public void RateHelpfulness(bool wasHelpful) => WasHelpful = wasHelpful;
}
