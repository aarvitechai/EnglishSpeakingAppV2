using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Practice;
using SpeakMate.Domain.Practice;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Sessions;

public sealed class SessionEndpointTests(TestDatabase database) : ApiTestBase(database)
{
    private static readonly object Onboarding = new
    {
        level = "Beginner",
        goals = new[] { "EverydayConversation" },
        supportLanguage = "en",
        dailyTargetMinutes = 10,
    };

    private async Task<HttpClient> OnboardedClientAsync()
    {
        var (client, _, _) = await SignedInClientAsync();
        (await client.PutAsJsonAsync("/api/v1/me/onboarding", Onboarding, Json, Ct)).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<SessionDto> StartAsync(HttpClient client, string mode = "DailyConversation",
        string? topic = "My weekend")
    {
        var response = await client.PostAsJsonAsync("/api/v1/sessions", new { mode, topic }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Guid sessionId, string text,
        string inputMethod = "Text", double? speechConfidence = null) =>
        client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/messages",
            new { text, inputMethod, speechConfidence }, Json, Ct);

    [Fact]
    public async Task Start_BeforeOnboarding_ReturnsBadRequest()
    {
        var (client, _, _) = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/sessions", new { mode = "RolePlay" }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("session.onboarding_required", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task FullConversation_StartReplyAndReload()
    {
        var client = await OnboardedClientAsync();

        var session = await StartAsync(client);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(Speaker.Coach, Assert.Single(session.Turns).Speaker);

        var reply = await SendAsync(client, session.Id, "I went to the beach.", "Voice", 0.92);
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        var updated = (await reply.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!;
        Assert.Equal([Speaker.Coach, Speaker.Learner, Speaker.Coach], updated.Turns.Select(t => t.Speaker));
        Assert.Equal(InputMethod.Voice, updated.Turns[1].InputMethod);

        var reloaded = await client.GetFromJsonAsync<SessionDto>($"/api/v1/sessions/{session.Id}", Json, Ct);
        Assert.Equal(3, reloaded!.Turns.Count);
        Assert.Equal(1, reloaded.LearnerTurnCount);

        await using var db = Database.CreateDbContext();
        Assert.Equal(2, await db.UsageEvents.CountAsync(u => u.SessionId == session.Id && u.Succeeded, Ct));
    }

    [Fact]
    public async Task OtherUser_CannotReadOrWriteSession()
    {
        var owner = await OnboardedClientAsync();
        var session = await StartAsync(owner);
        var intruder = await OnboardedClientAsync();

        var read = await intruder.GetAsync($"/api/v1/sessions/{session.Id}", Ct);
        var write = await SendAsync(intruder, session.Id, "Hijack!");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        await using var db = Database.CreateDbContext();
        Assert.Equal(1, await db.ConversationTurns.CountAsync(t => t.SessionId == session.Id, Ct));
    }

    [Fact]
    public async Task SendMessage_WithEmptyText_ReturnsValidationError()
    {
        var client = await OnboardedClientAsync();
        var session = await StartAsync(client);

        var response = await SendAsync(client, session.Id, "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Start_BeyondMaxActiveSessions_ReturnsConflict()
    {
        var client = await OnboardedClientAsync();
        for (var i = 0; i < 3; i++)
        {
            await StartAsync(client);
        }

        var response = await client.PostAsJsonAsync("/api/v1/sessions", new { mode = "RolePlay" }, Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Topics_AreListedForEveryMode()
    {
        var (client, _, _) = await SignedInClientAsync();

        var topics = await client.GetFromJsonAsync<List<TopicSuggestionsDto>>("/api/v1/sessions/topics", Json, Ct);

        Assert.Equal(Enum.GetValues<PracticeMode>().Length, topics!.Count);
        Assert.All(topics, t => Assert.NotEmpty(t.Topics));
    }

    [Fact]
    public async Task Start_WithTranscriptConsent_MarksSessionToKeepTranscript()
    {
        var client = await OnboardedClientAsync();
        await client.PutAsJsonAsync("/api/v1/me/consents/TranscriptStorage", new { granted = true }, Json, Ct);

        var session = await StartAsync(client);

        Assert.True(session.SaveTranscript);
    }
}

public sealed class SessionQuotaTests(TestDatabase database) : ApiTestBase(database)
{
    protected override SpeakMateApiFactory CreateFactory() => new(Database)
    {
        Settings = new Dictionary<string, string> { ["Usage:DailyTokenLimitPerUser"] = "1" },
    };

    [Fact]
    public async Task OverDailyQuota_ReturnsTooManyRequests()
    {
        var (client, _, _) = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/v1/me/onboarding", new
        {
            level = "Beginner", goals = new[] { "Travel" }, supportLanguage = "en", dailyTargetMinutes = 10,
        }, Json, Ct);

        // The first session is allowed (quota is checked before the call), and it consumes tokens.
        var first = await client.PostAsJsonAsync("/api/v1/sessions", new { mode = "RolePlay" }, Json, Ct);
        var second = await client.PostAsJsonAsync("/api/v1/sessions", new { mode = "RolePlay" }, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("usage.daily_limit", (await ReadJsonAsync(second)).GetProperty("code").GetString());
    }
}

public sealed class SessionAiFailureTests(TestDatabase database) : ApiTestBase(database)
{
    private sealed class FailingCoach : IAiCoach
    {
        public Task<CoachReply> GenerateReplyAsync(CoachContext context, CancellationToken cancellationToken = default) =>
            throw new AiProviderException("Simulated provider outage", new AiUsage("fake-coach", 10, 0, 5));

        public Task<FeedbackDraft> GenerateFeedbackAsync(CoachContext context, CancellationToken cancellationToken = default) =>
            throw new AiProviderException("Simulated provider outage");
    }

    protected override SpeakMateApiFactory CreateFactory() => new(Database)
    {
        ConfigureTestServices = services => services.AddSingleton<IAiCoach, FailingCoach>(),
    };

    [Fact]
    public async Task AiOutage_Returns503_PersistsNoSession_ButRecordsFailedUsage()
    {
        var (client, _, _) = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/v1/me/onboarding", new
        {
            level = "Beginner", goals = new[] { "Travel" }, supportLanguage = "en", dailyTargetMinutes = 10,
        }, Json, Ct);

        var response = await client.PostAsJsonAsync("/api/v1/sessions", new { mode = "RolePlay" }, Json, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("Simulated provider outage", body, StringComparison.Ordinal);

        await using var db = Database.CreateDbContext();
        Assert.False(await db.PracticeSessions.AnyAsync(Ct));
        var usage = await db.UsageEvents.SingleAsync(Ct);
        Assert.False(usage.Succeeded);
    }
}
