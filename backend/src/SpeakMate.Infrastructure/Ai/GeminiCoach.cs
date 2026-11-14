using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeakMate.Application.Abstractions;
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

    private readonly GeminiOptions _options = options.Value;

    public async Task<CoachReply> GenerateReplyAsync(CoachContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new GeminiRequest(
            new GeminiContent(null, [new GeminiPart(CoachPromptBuilder.BuildSystemInstruction(context))]),
            CoachPromptBuilder.BuildConversation(context)
                .Select(m => new GeminiContent(m.Speaker == Speaker.Coach ? "model" : "user", [new GeminiPart(m.Text)]))
                .ToList(),
            new GeminiGenerationConfig(
                "application/json",
                ReplySchema,
                _options.MaxOutputTokens,
                string.IsNullOrWhiteSpace(_options.ThinkingLevel) ? null : new GeminiThinkingConfig(_options.ThinkingLevel)));

        var stopwatch = Stopwatch.StartNew();
        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var usage = ToUsage(response, (int)stopwatch.ElapsedMilliseconds);

        var text = ExtractReply(response, usage);
        return new CoachReply(text, usage);
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
        if (response.PromptFeedback?.BlockReason is { } blockReason)
        {
            throw new AiProviderException($"The AI provider blocked the request ({blockReason}).", usage);
        }

        var candidate = response.Candidates is { Count: > 0 } candidates ? candidates[0] : null;
        var rawJson = string.Concat(candidate?.Content?.Parts?
            .Where(p => !p.Thought && p.Text is not null)
            .Select(p => p.Text) ?? []);

        if (candidate?.FinishReason is { } finish && finish is not "STOP")
        {
            throw new AiProviderException($"The AI reply was incomplete ({finish}).", usage);
        }

        CoachReplyPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CoachReplyPayload>(rawJson, WireJson);
        }
        catch (JsonException ex)
        {
            throw new AiProviderException("The AI reply did not match the expected format.", usage, ex);
        }

        var reply = payload?.Reply?.Trim();
        if (string.IsNullOrWhiteSpace(reply) || reply.Length > MaxReplyCharacters)
        {
            throw new AiProviderException("The AI reply was empty or too long.", usage);
        }

        return reply;
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
