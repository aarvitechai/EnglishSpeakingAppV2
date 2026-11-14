using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Feedback;

namespace SpeakMate.Application.Feedback;

/// <summary>
/// Turns untrusted AI output into valid domain feedback: drops malformed corrections, enforces
/// limits and lengths, and fills safe defaults. The model can never break a domain rule.
/// </summary>
public static class FeedbackSanitizer
{
    public const string DefaultSummary = "Great effort today! Keep practising a little every day.";
    public const string DefaultRepeatTask = "Try the same topic again and add one more detail to each answer.";

    public static SessionFeedback ToDomain(Guid sessionId, FeedbackDraft draft, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var corrections = (draft.Corrections ?? [])
            .Select(TryMap)
            .OfType<Correction>()
            .DistinctBy(c => (c.Original.ToUpperInvariant(), c.Improved.ToUpperInvariant()))
            .Where(c => !string.Equals(c.Original, c.Improved, StringComparison.OrdinalIgnoreCase))
            .Take(SessionFeedback.MaxCorrections)
            .ToList();

        var phrases = (draft.UsefulPhrases ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Clip(p.Trim(), SessionFeedback.PhraseMaxLength))
            .DistinctBy(p => p.ToUpperInvariant())
            .Take(SessionFeedback.MaxUsefulPhrases)
            .ToList();

        var summary = string.IsNullOrWhiteSpace(draft.Summary)
            ? DefaultSummary
            : Clip(draft.Summary.Trim(), SessionFeedback.SummaryMaxLength);
        var repeatTask = string.IsNullOrWhiteSpace(draft.RepeatTask)
            ? DefaultRepeatTask
            : Clip(draft.RepeatTask.Trim(), SessionFeedback.RepeatTaskMaxLength);

        return SessionFeedback.Create(sessionId, summary, corrections, phrases, repeatTask, now);
    }

    private static Correction? TryMap(CorrectionDraft? draft)
    {
        if (draft is null
            || !Enum.TryParse<CorrectionCategory>(draft.Category, ignoreCase: true, out var category)
            || !Enum.IsDefined(category)
            || string.IsNullOrWhiteSpace(draft.Original)
            || string.IsNullOrWhiteSpace(draft.Improved)
            || string.IsNullOrWhiteSpace(draft.Explanation))
        {
            return null;
        }

        return new Correction(category,
            Clip(draft.Original.Trim(), Correction.TextMaxLength),
            Clip(draft.Improved.Trim(), Correction.TextMaxLength),
            Clip(draft.Explanation.Trim(), Correction.TextMaxLength));
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd();
}
