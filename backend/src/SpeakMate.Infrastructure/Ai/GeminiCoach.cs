using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Feedback;
using SpeakMate.Application.Practice;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Infrastructure.Ai;

/// <summary>
/// Gemini implementation of <see cref="IAiCoach"/> over the REST generateContent endpoint.
/// The API key is sent in the x-goog-api-key header (never in the URL, so it can't leak into logs).
/// Prompt and reply text are never logged.
/// </summary>
internal sealed partial class GeminiCoach(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiCoach> logger)
    : IAiCoach
{
    /// <summary>Hard ceiling on accepted reply length; anything longer is treated as malformed output.</summary>
    internal const int MaxReplyCharacters = 1200;

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    private static readonly JsonElement ReplySchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "reply": { "type": "string", "description": "The coach's next message to the learner." }
          },
          "required": ["reply"],
          "additionalProperties": false
        }
        """).RootElement.Clone();

    private static readonly JsonElement FeedbackSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "summary": { "type": "string" },
            "corrections": {
              "type": "array",
              "maxItems": 3,
              "items": {
                "type": "object",
                "properties": {
                  "category": { "type": "string", "enum": ["Grammar", "Vocabulary", "NaturalPhrasing", "Fluency", "Pronunciation"] },
                  "original": { "type": "string" },
                  "improved": { "type": "string" },
                  "explanation": { "type": "string" }
                },
                "required": ["category", "original", "improved", "explanation"]
              }
            },
            "usefulPhrases": { "type": "array", "maxItems": 5, "items": { "type": "string" } },
            "repeatTask": { "type": "string" }
          },
          "required": ["summary", "corrections", "usefulPhrases", "repeatTask"]
        }
        """).RootElement.Clone();

    private readonly GeminiOptions _options = options.Value;

    public async Task<CoachReply> GenerateReplyAsync(CoachContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var contents = CoachPromptBuilder.BuildConversation(context)
            .Select(m => new GeminiContent(m.Speaker == Speaker.Coach ? "model" : "user", [new GeminiPart(m.Text)]))
            .ToList();

        var (response, usage) = await CallAsync(CoachPromptBuilder.BuildSystemInstruction(context), contents,
            ReplySchema, cancellationToken).ConfigureAwait(false);
        return new CoachReply(ExtractReply(response, usage), usage);
    }

    public async Task<FeedbackDraft> GenerateFeedbackAsync(CoachContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        GeminiContent[] contents =
        [
            new("user", [new GeminiPart("Review this practice conversation transcript:\n\n" +
                                        FeedbackPromptBuilder.BuildTranscript(context))]),
        ];

        var (response, usage) = await CallAsync(FeedbackPromptBuilder.BuildInstruction(context), contents,
            FeedbackSchema, cancellationToken).ConfigureAwait(false);
        var payload = ExtractPayload<FeedbackPayload>(response, usage);

        // Further sanitising (limits, categories, lengths) happens in the Application layer.
        return new FeedbackDraft(
            payload.Summary ?? string.Empty,
            (payload.Corrections ?? [])
                .Select(c => new CorrectionDraft(c.Category ?? "", c.Original ?? "", c.Improved ?? "", c.Explanation ?? ""))
                .ToList(),
            payload.UsefulPhrases ?? [],
            payload.RepeatTask ?? string.Empty,
            usage);
    }

    private async Task<(GeminiResponse Response, AiUsage Usage)> CallAsync(string systemInstruction,
        IReadOnlyList<GeminiContent> contents, JsonElement schema, CancellationToken cancellationToken)
    {
        var request = new GeminiRequest(
            new GeminiContent(null, [new GeminiPart(systemInstruction)]),
            contents,
            new GeminiGenerationConfig(
                "application/json",
                schema,
                _options.MaxOutputTokens,
                string.IsNullOrWhiteSpace(_options.ThinkingLevel) ? null : new GeminiThinkingConfig(_options.ThinkingLevel)));

        var stopwatch = Stopwatch.StartNew();
        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return (response, ToUsage(response, (int)stopwatch.ElapsedMilliseconds));
    }

    private async Task<GeminiResponse> SendAsync(GeminiRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri($"v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent", UriKind.Relative))
        {
            Content = JsonContent.Create(request, options: WireJson),
        };
        message.Headers.Add("x-goog-api-key", _options.ApiKey);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException
                                       or Polly.CircuitBreaker.BrokenCircuitException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AiProviderException("The AI provider could not be reached.", null, ex);
        }

        using (httpResponse)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                LogProviderError((int)httpResponse.StatusCode, _options.Model);
                throw new AiProviderException(httpResponse.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => "The AI provider rate limit was reached.",
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "The AI provider rejected the API key.",
                    HttpStatusCode.NotFound => "The configured AI model was not found.",
                    _ => "The AI provider returned an error.",
                });
            }

            try
            {
                return await httpResponse.Content.ReadFromJsonAsync<GeminiResponse>(WireJson, cancellationToken)
                           .ConfigureAwait(false)
                       ?? throw new AiProviderException("The AI provider returned an empty response.");
            }
            catch (JsonException ex)
            {
                throw new AiProviderException("The AI provider returned an unreadable response.", null, ex);
            }
        }
    }

    internal static string ExtractReply(GeminiResponse response, AiUsage usage)
    {
        var reply = ExtractPayload<CoachReplyPayload>(response, usage).Reply?.Trim();
        if (string.IsNullOrWhiteSpace(reply) || reply.Length > MaxReplyCharacters)
        {
            throw new AiProviderException("The AI reply was empty or too long.", usage);
        }

        return reply;
    }

    /// <summary>Checks blocking/finish state and deserialises the JSON the model produced.</summary>
    internal static TPayload ExtractPayload<TPayload>(GeminiResponse response, AiUsage usage)
        where TPayload : class
    {
        if (response.PromptFeedback?.BlockReason is { } blockReason)
        {
            throw new AiProviderException($"The AI provider blocked the request ({blockReason}).", usage);
        }

        var candidate = response.Candidates is { Count: > 0 } candidates ? candidates[0] : null;
        if (candidate?.FinishReason is { } finish && finish is not "STOP")
        {
            throw new AiProviderException($"The AI reply was incomplete ({finish}).", usage);
        }

        var rawJson = string.Concat(candidate?.Content?.Parts?
            .Where(p => !p.Thought && p.Text is not null)
            .Select(p => p.Text) ?? []);

        try
        {
            return JsonSerializer.Deserialize<TPayload>(rawJson, WireJson)
                   ?? throw new AiProviderException("The AI reply was empty.", usage);
        }
        catch (JsonException ex)
        {
            throw new AiProviderException("The AI reply did not match the expected format.", usage, ex);
        }
    }

    private AiUsage ToUsage(GeminiResponse response, int durationMs)
    {
        var meta = response.UsageMetadata;
        // Thinking tokens are billed as output tokens.
        return new AiUsage(
            _options.Model,
            meta?.PromptTokenCount ?? 0,
            (meta?.CandidatesTokenCount ?? 0) + (meta?.ThoughtsTokenCount ?? 0),
            durationMs);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini returned HTTP {StatusCode} for model {Model}")]
    private partial void LogProviderError(int statusCode, string model);
}
