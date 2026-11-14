using SpeakMate.Domain.Common;

namespace SpeakMate.Domain.Usage;

public enum AiOperation
{
    ConversationReply = 1,
    SessionFeedback = 2,
}

/// <summary>
/// One AI provider call, recorded for cost visibility and per-user quotas.
/// Contains metadata only: never prompt or response text.
/// </summary>
public sealed class UsageEvent : Entity
{
    public const int ModelMaxLength = 100;

    private UsageEvent() { } // EF Core

    private UsageEvent(Guid userId, Guid? sessionId, AiOperation operation, string model, int inputTokens,
        int outputTokens, decimal estimatedCostUsd, bool succeeded, int durationMs, DateTimeOffset now)
    {
        UserId = Guard.NotEmpty(userId, "usage.user_required");
        SessionId = sessionId;
        Operation = operation;
        Model = Guard.NotBlank(model, "usage.model_required", "Model is required.", ModelMaxLength);
        InputTokens = Math.Max(0, inputTokens);
        OutputTokens = Math.Max(0, outputTokens);
        EstimatedCostUsd = Math.Max(0, estimatedCostUsd);
        Succeeded = succeeded;
        DurationMs = Math.Max(0, durationMs);
        CreatedAt = now;
    }

    public Guid UserId { get; private set; }
    public Guid? SessionId { get; private set; }
    public AiOperation Operation { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public bool Succeeded { get; private set; }
    public int DurationMs { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public int TotalTokens => InputTokens + OutputTokens;

    public static UsageEvent Record(Guid userId, Guid? sessionId, AiOperation operation, string model,
        int inputTokens, int outputTokens, decimal estimatedCostUsd, bool succeeded, int durationMs,
        DateTimeOffset now) =>
        new(userId, sessionId, operation, model, inputTokens, outputTokens, estimatedCostUsd, succeeded,
            durationMs, now);
}
