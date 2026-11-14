using System.Net;
using System.Net.Http.Json;
using SpeakMate.Application.Consents;
using SpeakMate.Application.Profiles;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Learners;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Me;

public sealed class MeEndpointTests(TestDatabase database) : ApiTestBase(database)
{
    private static object ValidPreferences(string supportLanguage = "hi") => new
    {
        level = "Intermediate",
        goals = new[] { "Work", "Interviews" },
        supportLanguage,
        dailyTargetMinutes = 15,
    };

    [Fact]
    public async Task GetMe_ReturnsOwnProfile()
    {
        var (client, _, _) = await SignedInClientAsync("Meera");

        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/v1/me", Json, Ct);

        Assert.Equal("Meera", profile!.DisplayName);
        Assert.False(profile.OnboardingCompleted);
    }

    [Fact]
    public async Task Onboarding_SavesPreferencesAndMarksComplete()
    {
        var (client, _, _) = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/me/onboarding", ValidPreferences(), Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>(Json, Ct);
        Assert.True(profile!.OnboardingCompleted);
        Assert.Equal(EnglishLevel.Intermediate, profile.Level);
        Assert.Equal([LearningGoal.Work, LearningGoal.Interviews], profile.Goals);
        Assert.Equal("hi", profile.SupportLanguage);
    }

    [Fact]
    public async Task Onboarding_WithUnsupportedLanguage_ReturnsValidationError()
    {
        var (client, _, _) = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/me/onboarding", ValidPreferences("xx"), Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadJsonAsync(response)).GetProperty("errors").TryGetProperty("supportLanguage", out _));
    }

    [Fact]
    public async Task Onboarding_WithUnknownEnumValue_ReturnsBadRequestNotServerError()
    {
        var (client, _, _) = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/me/onboarding",
            new { level = "Wizard", goals = new[] { "Work" }, supportLanguage = "en", dailyTargetMinutes = 10 }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EachUser_SeesOnlyTheirOwnProfile()
    {
        var (alice, _, _) = await SignedInClientAsync("Alice");
        var (bob, _, _) = await SignedInClientAsync("Bob");
        await alice.PutAsJsonAsync("/api/v1/me/display-name", new { displayName = "Alice Updated" }, Json, Ct);

        var bobsView = await bob.GetFromJsonAsync<ProfileDto>("/api/v1/me", Json, Ct);

        Assert.Equal("Bob", bobsView!.DisplayName);
    }

    [Fact]
    public async Task Consents_DefaultToTermsOnly_AndOptionalOnesCanBeToggled()
    {
        var (client, _, _) = await SignedInClientAsync();

        var initial = await client.GetFromJsonAsync<List<ConsentStatusDto>>("/api/v1/me/consents", Json, Ct);
        Assert.True(initial!.Single(c => c.Type == ConsentType.TermsAndPrivacy).Granted);
        Assert.False(initial!.Single(c => c.Type == ConsentType.TranscriptStorage).Granted);

        var grant = await client.PutAsJsonAsync("/api/v1/me/consents/TranscriptStorage", new { granted = true }, Json, Ct);
        Assert.True((await grant.Content.ReadFromJsonAsync<ConsentStatusDto>(Json, Ct))!.Granted);

        var withdraw = await client.PutAsJsonAsync("/api/v1/me/consents/TranscriptStorage", new { granted = false }, Json, Ct);
        Assert.False((await withdraw.Content.ReadFromJsonAsync<ConsentStatusDto>(Json, Ct))!.Granted);
    }

    [Fact]
    public async Task Consents_TermsCannotBeWithdrawn()
    {
        var (client, _, _) = await SignedInClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/me/consents/TermsAndPrivacy", new { granted = false }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
