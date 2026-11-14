using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;
using SpeakMate.Infrastructure.Ai;

namespace SpeakMate.UnitTests.Infrastructure;

/// <summary>Tests the Gemini adapter against canned HTTP responses; no network calls are made.</summary>
public sealed class GeminiCoachTests
{
    private const string ApiKey = "test-api-key-123";

    private static readonly CoachContext Context = new(PracticeMode.DailyConversation, EnglishLevel.Beginner,
        "Your weekend", [LearningGoal.Confidence], "en",
        [
            new CoachTurn(Speaker.Coach, "Hi! What did you do?", InputMethod.Text, null),
            new CoachTurn(Speaker.Learner, "I play football.", InputMethod.Voice, 0.9),
        ]);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (GeminiCoach Coach, StubHandler Handler) Create(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") };
        var options = Options.Create(new GeminiOptions { ApiKey = ApiKey, Model = "test-model", ThinkingLevel = "LOW" });
        return (new GeminiCoach(http, options, NullLogger<GeminiCoach>.Instance), handler);
    }

    private static string SuccessBody(string replyJson, string finishReason = "STOP", bool withThought = false)
    {
        var parts = new List<object>();
        if (withThought)
        {
            parts.Add(new { text = "internal reasoning", thought = true });
        }

        parts.Add(new { text = replyJson });
        return JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { role = "model", parts }, finishReason } },
            usageMetadata = new { promptTokenCount = 120, candidatesTokenCount = 30, thoughtsTokenCount = 15, totalTokenCount = 165 },
        });
    }

    [Fact]
    public async Task SendsKeyInHeaderNotUrl_AndBuildsExpectedRequest()
    {
        var (coach, handler) = Create(SuccessBody("""{"reply":"Great! Who did you play with?"}"""));

        await coach.GenerateReplyAsync(Context, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/v1beta/models/test-model:generateContent", handler.Request.RequestUri!.AbsolutePath);
        Assert.DoesNotContain(ApiKey, handler.Request.RequestUri.ToString(), StringComparison.Ordinal);
        Assert.Equal(ApiKey, handler.Request.Headers.GetValues("x-goog-api-key").Single());

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        var roles = root.GetProperty("contents").EnumerateArray().Select(c => c.GetProperty("role").GetString()).ToArray();
        Assert.Equal(["user", "model", "user"], roles);
        Assert.Equal("application/json", root.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
        Assert.Equal("object", root.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("type").GetString());
        Assert.Equal("LOW", root.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.Contains("ONE question", root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParsesReplyAndCountsThinkingTokensAsOutput()
    {
        var (coach, _) = Create(SuccessBody("""{"reply":"  Great! Who did you play with?  "}""", withThought: true));

        var reply = await coach.GenerateReplyAsync(Context, TestContext.Current.CancellationToken);

        Assert.Equal("Great! Who did you play with?", reply.Text);
        Assert.Equal("test-model", reply.Usage.Model);
        Assert.Equal(120, reply.Usage.InputTokens);
        Assert.Equal(45, reply.Usage.OutputTokens);
    }

    [Fact]
    public async Task BlockedPrompt_ThrowsWithUsage()
    {
        var body = JsonSerializer.Serialize(new
        {
            promptFeedback = new { blockReason = "SAFETY" },
            usageMetadata = new { promptTokenCount = 50, candidatesTokenCount = 0, thoughtsTokenCount = 0, totalTokenCount = 50 },
        });
        var (coach, _) = Create(body);

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            coach.GenerateReplyAsync(Context, TestContext.Current.CancellationToken));

        Assert.Contains("SAFETY", ex.Message, StringComparison.Ordinal);
        Assert.Equal(50, ex.Usage!.InputTokens);
    }

    [Theory]
    [InlineData("MAX_TOKENS", """{"reply":"Gre""")]
    [InlineData("STOP", "this is not json")]
    [InlineData("STOP", """{"reply":""}""")]
    [InlineData("STOP", """{"other":"field"}""")]
    public async Task IncompleteOrMalformedOutput_Throws(string finishReason, string replyJson)
    {
        var (coach, _) = Create(SuccessBody(replyJson, finishReason));

        await Assert.ThrowsAsync<AiProviderException>(() =>
            coach.GenerateReplyAsync(Context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OverlongReply_IsRejected()
    {
        var longReply = JsonSerializer.Serialize(new { reply = new string('a', GeminiCoach.MaxReplyCharacters + 1) });
        var (coach, _) = Create(SuccessBody(longReply));

        await Assert.ThrowsAsync<AiProviderException>(() =>
            coach.GenerateReplyAsync(Context, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "API key")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate limit")]
    [InlineData(HttpStatusCode.NotFound, "model")]
    [InlineData(HttpStatusCode.InternalServerError, "error")]
    public async Task HttpErrors_BecomeProviderExceptionsWithoutLeakingBody(HttpStatusCode status, string expected)
    {
        var (coach, _) = Create("""{"error":{"message":"secret internal detail"}}""", status);

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            coach.GenerateReplyAsync(Context, TestContext.Current.CancellationToken));

        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret internal detail", ex.Message, StringComparison.Ordinal);
    }
}
