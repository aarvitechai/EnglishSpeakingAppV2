using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakMate.Infrastructure.Ai;

// Wire types for POST v1beta/models/{model}:generateContent. Only the fields this app uses are modelled;
// unknown response fields are ignored, so additive API changes don't break deserialisation.

internal sealed record GeminiRequest(
    [property: JsonPropertyName("systemInstruction")] GeminiContent SystemInstruction,
    [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
    [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

internal sealed record GeminiContent(
    [property: JsonPropertyName("role")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role,
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

internal sealed record GeminiPart(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("thought")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Thought = false);

internal sealed record GeminiGenerationConfig(
    [property: JsonPropertyName("responseMimeType")] string ResponseMimeType,
    [property: JsonPropertyName("responseJsonSchema")] JsonElement ResponseJsonSchema,
    [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
    [property: JsonPropertyName("thinkingConfig")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GeminiThinkingConfig? ThinkingConfig);

internal sealed record GeminiThinkingConfig([property: JsonPropertyName("thinkingLevel")] string ThinkingLevel);

internal sealed record GeminiResponse(
    [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates,
    [property: JsonPropertyName("promptFeedback")] GeminiPromptFeedback? PromptFeedback,
    [property: JsonPropertyName("usageMetadata")] GeminiUsageMetadata? UsageMetadata,
    [property: JsonPropertyName("modelVersion")] string? ModelVersion);

internal sealed record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content,
    [property: JsonPropertyName("finishReason")] string? FinishReason);

internal sealed record GeminiPromptFeedback([property: JsonPropertyName("blockReason")] string? BlockReason);

internal sealed record GeminiUsageMetadata(
    [property: JsonPropertyName("promptTokenCount")] int PromptTokenCount,
    [property: JsonPropertyName("candidatesTokenCount")] int CandidatesTokenCount,
    [property: JsonPropertyName("thoughtsTokenCount")] int ThoughtsTokenCount,
    [property: JsonPropertyName("totalTokenCount")] int TotalTokenCount);

/// <summary>Structured output the coach must return.</summary>
internal sealed record CoachReplyPayload([property: JsonPropertyName("reply")] string? Reply);
