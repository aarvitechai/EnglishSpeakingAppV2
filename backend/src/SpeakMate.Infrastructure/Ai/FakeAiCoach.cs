using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Infrastructure.Ai;

/// <summary>
/// Deterministic offline coach for automated tests and for running the app without an API key.
/// It follows the same contract as the real coach (one question per reply) but is clearly labelled.
/// </summary>
public sealed class FakeAiCoach : IAiCoach
{
    public const string ModelName = "fake-coach";

    private static readonly string[] FollowUps =
    [
        "That's interesting! Can you tell me a little more about it?",
        "Thanks for sharing. Why do you think that is?",
        "Nice. What happened next?",
        "Good answer. How did that make you feel?",
        "I see. Can you give me an example?",
    ];

    public Task<CoachReply> GenerateReplyAsync(CoachContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var learnerTurns = context.Turns.Count(t => t.Speaker == Speaker.Learner);
        var text = learnerTurns == 0
            ? $"Hello! I'm your practice coach (offline demo mode). Today's topic is \"{context.Topic}\". What would you like to say about it?"
            : FollowUps[(learnerTurns - 1) % FollowUps.Length];

        var inputTokens = context.Turns.Sum(t => t.Text.Length) / 4 + 200;
        return Task.FromResult(new CoachReply(text, new AiUsage(ModelName, inputTokens, text.Length / 4, 1)));
    }

    public Task<FeedbackDraft> GenerateFeedbackAsync(CoachContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var firstLearnerLine = context.Turns.FirstOrDefault(t => t.Speaker == Speaker.Learner)?.Text ?? "Hello";
        var corrections = new List<CorrectionDraft>
        {
            new("NaturalPhrasing", firstLearnerLine, firstLearnerLine.TrimEnd('.', '!', '?') + ", actually.",
                "Demo correction from offline mode: a softener such as actually can sound more natural."),
        };

        return Task.FromResult(new FeedbackDraft(
            $"Nice work practising \"{context.Topic}\" (offline demo feedback). You kept the conversation going well.",
            corrections,
            ["Could you say that again, please?", "In my opinion...", "That reminds me of..."],
            "Answer the first question again and add one more detail.",
            new AiUsage(ModelName, context.Turns.Sum(t => t.Text.Length) / 4 + 300, 120, 1)));
    }
}
