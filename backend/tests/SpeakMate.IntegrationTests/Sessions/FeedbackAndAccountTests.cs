using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SpeakMate.Application.Account;
using SpeakMate.Application.Practice;
using SpeakMate.Application.Progress;
using SpeakMate.Domain.Practice;
using SpeakMate.Infrastructure.Retention;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Sessions;

/// <summary>Shared helpers for tests that need an onboarded learner with a conversation.</summary>
public abstract class ConversationTestBase(TestDatabase database) : ApiTestBase(database)
{
    protected async Task<HttpClient> OnboardedClientAsync(bool keepTranscripts = false)
    {
        var (client, _, _) = await SignedInClientAsync();
        (await client.PutAsJsonAsync("/api/v1/me/onboarding", new
        {
            level = "Intermediate", goals = new[] { "Work" }, supportLanguage = "en", dailyTargetMinutes = 15,
        }, Json, Ct)).EnsureSuccessStatusCode();

        if (keepTranscripts)
        {
            (await client.PutAsJsonAsync("/api/v1/me/consents/TranscriptStorage", new { granted = true }, Json, Ct))
                .EnsureSuccessStatusCode();
        }

        return client;
    }

    protected static async Task<SessionDto> StartWithReplyAsync(HttpClient client, string topic = "Team meetings")
    {
        var start = await client.PostAsJsonAsync("/api/v1/sessions", new { mode = "DailyConversation", topic }, Json, Ct);
        var session = (await start.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!;
        var reply = await client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/messages",
            new { text = "I have many meeting every day.", inputMethod = "Text" }, Json, Ct);
        reply.EnsureSuccessStatusCode();
        return (await reply.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!;
    }

    protected static async Task<SessionDto> FinishAsync(HttpClient client, Guid sessionId)
    {
        var response = await client.PostAsync($"/api/v1/sessions/{sessionId}/finish", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!;
    }
}

public sealed class FeedbackEndpointTests(TestDatabase database) : ConversationTestBase(database)
{
    [Fact]
    public async Task Finish_ReturnsFeedback_AndClearsTranscriptWithoutConsent()
    {
        var client = await OnboardedClientAsync(keepTranscripts: false);
        var session = await StartWithReplyAsync(client);

        var finished = await FinishAsync(client, session.Id);

        Assert.Equal(SessionStatus.Completed, finished.Status);
        Assert.NotNull(finished.Feedback);
        Assert.InRange(finished.Feedback.Corrections.Count, 0, 3);
        Assert.Empty(finished.Turns);
        Assert.True(finished.TranscriptCleared);

        await using var db = Database.CreateDbContext();
        Assert.Equal(0, await db.ConversationTurns.CountAsync(t => t.SessionId == session.Id, Ct));
        Assert.True(await db.SessionFeedback.AnyAsync(f => f.SessionId == session.Id, Ct));
    }

    [Fact]
    public async Task Finish_WithConsent_KeepsTranscript()
    {
        var client = await OnboardedClientAsync(keepTranscripts: true);
        var session = await StartWithReplyAsync(client);

        var finished = await FinishAsync(client, session.Id);

        Assert.Equal(3, finished.Turns.Count);
        Assert.False(finished.TranscriptCleared);
    }

    [Fact]
    public async Task Finish_Twice_IsIdempotent_AndSendingAfterwardsIsConflict()
    {
        var client = await OnboardedClientAsync(keepTranscripts: true);
        var session = await StartWithReplyAsync(client);

        var first = await FinishAsync(client, session.Id);
        var second = await FinishAsync(client, session.Id);
        var send = await client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/messages",
            new { text = "More?", inputMethod = "Text" }, Json, Ct);

        Assert.Equal(first.Feedback!.CreatedAt, second.Feedback!.CreatedAt);
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);
    }

    [Fact]
    public async Task Finish_WithoutAnyLearnerMessage_ReturnsBadRequest()
    {
        var client = await OnboardedClientAsync();
        var start = await client.PostAsJsonAsync("/api/v1/sessions", new { mode = "RolePlay" }, Json, Ct);
        var session = (await start.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!;

        var response = await client.PostAsync($"/api/v1/sessions/{session.Id}/finish", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("session.empty", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task RateFeedback_IsStored()
    {
        var client = await OnboardedClientAsync();
        var session = await StartWithReplyAsync(client);
        await FinishAsync(client, session.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/sessions/{session.Id}/feedback/rating",
            new { wasHelpful = true }, Json, Ct);

        Assert.True((await response.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!.Feedback!.WasHelpful);
    }

    [Fact]
    public async Task Abandon_EndsSessionWithoutFeedback()
    {
        var client = await OnboardedClientAsync();
        var session = await StartWithReplyAsync(client);

        var response = await client.PostAsync($"/api/v1/sessions/{session.Id}/abandon", null, Ct);
        var abandoned = (await response.Content.ReadFromJsonAsync<SessionDto>(Json, Ct))!;

        Assert.Equal(SessionStatus.Abandoned, abandoned.Status);
        Assert.Null(abandoned.Feedback);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/v1/sessions/{session.Id}/finish", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task History_IsPagedNewestFirst_AndScopedToUser()
    {
        var client = await OnboardedClientAsync();
        var older = await StartWithReplyAsync(client, "First topic");
        await FinishAsync(client, older.Id);
        var newer = await StartWithReplyAsync(client, "Second topic");
        var other = await OnboardedClientAsync();
        await StartWithReplyAsync(other, "Someone else");

        var page = await client.GetFromJsonAsync<PagedResult<SessionSummaryDto>>("/api/v1/sessions?page=1&pageSize=1", Json, Ct);

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(2, page.TotalPages);
        var item = Assert.Single(page.Items);
        Assert.Equal(newer.Id, item.Id);
        Assert.Equal(1, item.LearnerTurnCount);

        var second = await client.GetFromJsonAsync<PagedResult<SessionSummaryDto>>("/api/v1/sessions?page=2&pageSize=1", Json, Ct);
        Assert.True(Assert.Single(second!.Items).HasFeedback);
    }

    [Fact]
    public async Task History_WithInvalidPaging_ReturnsBadRequest()
    {
        var client = await OnboardedClientAsync();

        var response = await client.GetAsync("/api/v1/sessions?page=0&pageSize=500", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Progress_ReflectsCompletedPractice()
    {
        var client = await OnboardedClientAsync();
        var session = await StartWithReplyAsync(client);
        await FinishAsync(client, session.Id);

        var progress = await client.GetFromJsonAsync<ProgressDto>("/api/v1/progress?utcOffsetMinutes=330", Json, Ct);

        Assert.Equal(1, progress!.CompletedSessions);
        Assert.Equal(15, progress.DailyTargetMinutes);
        Assert.Equal(1, progress.CurrentStreakDays);
        Assert.Equal(7, progress.Last7Days.Count);
        Assert.NotEmpty(progress.FocusAreas);
    }
}

public sealed class AccountEndpointTests(TestDatabase database) : ConversationTestBase(database)
{
    [Fact]
    public async Task Export_ContainsProfileConsentsAndSessions()
    {
        var client = await OnboardedClientAsync(keepTranscripts: true);
        var session = await StartWithReplyAsync(client);
        await FinishAsync(client, session.Id);

        var response = await client.GetAsync("/api/v1/me/export", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        var export = JsonSerializer.Deserialize<AccountExportDto>(await response.Content.ReadAsStringAsync(Ct), Json)!;
        Assert.Equal("Test Learner", export.Profile.DisplayName);
        Assert.Equal(2, export.Consents.Count);
        var exported = Assert.Single(export.Sessions);
        Assert.Equal(3, exported.Turns.Count);
        Assert.NotNull(exported.Feedback);
    }

    [Fact]
    public async Task Delete_WithWrongPassword_IsRejectedAndKeepsData()
    {
        var client = await OnboardedClientAsync();

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me")
        {
            Content = JsonContent.Create(new { password = "not-my-password" }, options: Json),
        };
        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = Database.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(Ct));
    }

    [Fact]
    public async Task Delete_WithPassword_RemovesEverything_AndBlocksLogin()
    {
        var (client, _, email) = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/v1/me/onboarding", new
        {
            level = "Beginner", goals = new[] { "Travel" }, supportLanguage = "en", dailyTargetMinutes = 10,
        }, Json, Ct);
        var session = await StartWithReplyAsync(client);
        await FinishAsync(client, session.Id);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me")
        {
            Content = JsonContent.Create(new { password = DefaultPassword }, options: Json),
        };
        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using (var db = Database.CreateDbContext())
        {
            Assert.False(await db.Users.AnyAsync(Ct));
            Assert.False(await db.LearnerProfiles.AnyAsync(Ct));
            Assert.False(await db.PracticeSessions.AnyAsync(Ct));
            Assert.False(await db.SessionFeedback.AnyAsync(Ct));
            Assert.False(await db.UsageEvents.AnyAsync(Ct));
            Assert.False(await db.ConsentRecords.AnyAsync(Ct));
            Assert.False(await db.RefreshTokens.AnyAsync(Ct));
        }

        var login = await NewClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = DefaultPassword }, Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}

public sealed class RetentionTests(TestDatabase database) : ConversationTestBase(database)
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    protected override SpeakMateApiFactory CreateFactory() => new(Database)
    {
        ConfigureTestServices = services => services.AddSingleton<TimeProvider>(_clock),
    };

    private async Task<RetentionReport> RunRetentionAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RetentionService>().RunOnceAsync(Ct);
    }

    [Fact]
    public async Task IdleSessions_AreClosed_AndUnconsentedTranscriptsCleared()
    {
        var client = await OnboardedClientAsync(keepTranscripts: false);
        var session = await StartWithReplyAsync(client);

        _clock.Advance(TimeSpan.FromHours(3));
        var report = await RunRetentionAsync();

        Assert.Equal(1, report.SessionsClosed);
        Assert.Equal(1, report.TranscriptsCleared);
        await using var db = Database.CreateDbContext();
        var stored = await db.PracticeSessions.SingleAsync(s => s.Id == session.Id, Ct);
        Assert.Equal(SessionStatus.Abandoned, stored.Status);
        Assert.Equal(0, await db.ConversationTurns.CountAsync(Ct));
    }

    [Fact]
    public async Task CompletedSessionWithFailedFeedback_LosesTranscriptAfterGracePeriodOnly()
    {
        var client = await OnboardedClientAsync(keepTranscripts: false);
        var session = await StartWithReplyAsync(client);
        await using (var db = Database.CreateDbContext())
        {
            // Simulate "finished, but feedback generation failed": completed, no feedback, transcript kept.
            var stored = await db.PracticeSessions.Include(s => s.Turns).SingleAsync(s => s.Id == session.Id, Ct);
            stored.Complete(_clock.GetUtcNow());
            await db.SaveChangesAsync(Ct);
        }

        _clock.Advance(TimeSpan.FromHours(23));
        Assert.Equal(0, (await RunRetentionAsync()).TranscriptsCleared);

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(1, (await RunRetentionAsync()).TranscriptsCleared);
    }

    [Fact]
    public async Task ConsentedTranscripts_AreNeverCleared()
    {
        var client = await OnboardedClientAsync(keepTranscripts: true);
        var session = await StartWithReplyAsync(client);
        await FinishAsync(client, session.Id);

        _clock.Advance(TimeSpan.FromDays(30));
        await RunRetentionAsync();

        await using var db = Database.CreateDbContext();
        Assert.Equal(3, await db.ConversationTurns.CountAsync(t => t.SessionId == session.Id, Ct));
    }

    [Fact]
    public async Task DeadRefreshTokens_AreDeleted()
    {
        await SignedInClientAsync();

        _clock.Advance(TimeSpan.FromDays(30));
        var report = await RunRetentionAsync();

        Assert.Equal(1, report.RefreshTokensDeleted);
    }
}
