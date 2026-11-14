using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Application.Abstractions;

/// <summary>Everything the coach needs to know to produce its next message. Contains no account identifiers.</summary>
public sealed record CoachContext(
    PracticeMode Mode,
    EnglishLevel Level,
    string Topic,
    IReadOnlyList<LearningGoal> Goals,
    string SupportLanguage,
    IReadOnlyList<CoachTurn> Turns);

public sealed record CoachTurn(Speaker Speaker, string Text, InputMethod InputMethod, double? SpeechConfidence);

/// <summary>Provider-reported usage for one AI call.</summary>
public sealed record AiUsage(string Model, int InputTokens, int OutputTokens, int DurationMs);

public sealed record CoachReply(string Text, AiUsage Usage);

/// <summary>Raw feedback as proposed by the AI. Untrusted: sanitised before it becomes a domain object.</summary>
public sealed record FeedbackDraft(
    string Summary,
    IReadOnlyList<CorrectionDraft> Corrections,
    IReadOnlyList<string> UsefulPhrases,
    string RepeatTask,
    AiUsage Usage);

/// <summary>One correction proposed by the AI.</summary>
/// <param name="Category">One of Grammar, Vocabulary, NaturalPhrasing, Fluency, Pronunciation (as text).</param>
/// <param name="Original">What the learner said.</param>
/// <param name="Improved">A more natural or correct version.</param>
/// <param name="Explanation">A short, friendly reason.</param>
public sealed record CorrectionDraft(string Category, string Original, string Improved, string Explanation);

/// <summary>
/// Port to the AI provider. Implementations live in Infrastructure (Gemini, or a fake for tests
/// and offline development). The rest of the app never sees provider SDKs, keys or wire formats.
/// </summary>
public interface IAiCoach
{
    /// <exception cref="AiProviderException">The provider failed, timed out, refused, or returned invalid output.</exception>
    Task<CoachReply> GenerateReplyAsync(CoachContext context, CancellationToken cancellationToken = default);

    /// <summary>End-of-session feedback over the whole conversation.</summary>
    /// <exception cref="AiProviderException">The provider failed, timed out, refused, or returned invalid output.</exception>
    Task<FeedbackDraft> GenerateFeedbackAsync(CoachContext context, CancellationToken cancellationToken = default);
}

/// <summary>An AI call failed. <see cref="Usage"/> is set when tokens were still consumed (so cost is recorded).</summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException() : base("The AI provider request failed.") { }

    public AiProviderException(string message) : base(message) { }

    public AiProviderException(string message, Exception innerException) : base(message, innerException) { }

    public AiProviderException(string message, AiUsage? usage, Exception? innerException = null)
        : base(message, innerException) => Usage = usage;

    public AiUsage? Usage { get; }
}
