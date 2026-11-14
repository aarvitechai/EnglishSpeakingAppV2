using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SpeakMate.Api.Endpoints;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Auth;

public sealed class AuthEndpointTests(TestDatabase database) : ApiTestBase(database)
{
    private static string RefreshCookieHeader(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AuthEndpoints.RefreshCookieName, StringComparison.Ordinal));

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Json, Ct);

    [Fact]
    public async Task Register_CreatesAccountProfileAndTermsConsent()
    {
        var client = NewClient();
        var email = UniqueEmail();

        var response = await RegisterAsync(client, email, displayName: "Asha");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, Ct);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.Equal(email, auth.User.Email);
        Assert.Equal("Asha", auth.User.DisplayName);
        Assert.False(auth.User.OnboardingCompleted);

        await using var db = Database.CreateDbContext();
        Assert.True(await db.LearnerProfiles.AnyAsync(p => p.UserId == auth.User.Id, Ct));
        Assert.True(await db.ConsentRecords.AnyAsync(c => c.UserId == auth.User.Id, Ct));
    }

    [Fact]
    public async Task Register_SetsHardenedRefreshCookie_AndNeverReturnsRefreshTokenInBody()
    {
        var response = await RegisterAsync(NewClient(), UniqueEmail());

        var cookie = RefreshCookieHeader(response).ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.Ordinal);

        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("refresh", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_StoresOnlyHashOfRefreshToken()
    {
        var response = await RegisterAsync(NewClient(), UniqueEmail());
        var rawToken = RefreshCookieHeader(response).Split(';')[0].Split('=', 2)[1];

        await using var db = Database.CreateDbContext();
        var stored = await db.RefreshTokens.SingleAsync(Ct);
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        var email = UniqueEmail();
        await RegisterAsync(NewClient(), email);

        var response = await RegisterAsync(NewClient(), email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.email_taken", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("not-an-email", DefaultPassword, true, "email")]
    [InlineData("valid@example.test", "short", true, "password")]
    [InlineData("valid@example.test", DefaultPassword, false, "acceptTerms")]
    public async Task Register_WithInvalidInput_ReturnsFieldErrors(string email, string password, bool acceptTerms,
        string expectedField)
    {
        var response = await RegisterAsync(NewClient(), email, password, acceptTerms: acceptTerms);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty(expectedField, out _), $"Expected an error for '{expectedField}'.");
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var email = UniqueEmail();
        await RegisterAsync(NewClient(), email);

        var response = await LoginAsync(NewClient(), email, DefaultPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull((await response.Content.ReadFromJsonAsync<AuthResponse>(Json, Ct))!.AccessToken);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnSameGenericError()
    {
        var email = UniqueEmail();
        await RegisterAsync(NewClient(), email);

        var wrongPassword = await LoginAsync(NewClient(), email, "wrong-password-123");
        var unknownEmail = await LoginAsync(NewClient(), UniqueEmail(), "wrong-password-123");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal((await ReadJsonAsync(wrongPassword)).GetProperty("title").GetString(),
            (await ReadJsonAsync(unknownEmail)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_AfterFiveFailures_LocksAccountEvenForCorrectPassword()
    {
        var email = UniqueEmail();
        await RegisterAsync(NewClient(), email);

        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(NewClient(), email, "wrong-password-123");
        }

        var response = await LoginAsync(NewClient(), email, DefaultPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("auth.locked_out", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesToken()
    {
        var client = NewClient();
        var register = await RegisterAsync(client, UniqueEmail());
        var firstCookie = RefreshCookieHeader(register);

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null, Ct);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.NotEqual(firstCookie, RefreshCookieHeader(refresh));
        Assert.NotNull((await refresh.Content.ReadFromJsonAsync<AuthResponse>(Json, Ct))!.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReusingSpentToken_RevokesWholeFamily()
    {
        var client = NewClient();
        var register = await RegisterAsync(client, UniqueEmail());
        var stolenCookie = RefreshCookieHeader(register).Split(';')[0];

        // Legitimate client rotates the token.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/refresh", null, Ct)).StatusCode);

        // Attacker replays the old (spent) token.
        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replay.Headers.Add("Cookie", stolenCookie);
        var attacker = Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.SendAsync(replay, Ct)).StatusCode);

        // The legitimate client's newer token was revoked too, forcing a fresh sign-in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/refresh", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        var response = await NewClient().PostAsync("/api/v1/auth/refresh", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = NewClient();
        await RegisterAsync(client, UniqueEmail());
        var cookieBeforeLogout = client.DefaultRequestHeaders.Contains("Cookie");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null, Ct)).StatusCode);

        Assert.False(cookieBeforeLogout);
        await using var db = Database.CreateDbContext();
        Assert.True(await db.RefreshTokens.AllAsync(t => t.RevokedAt != null, Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/refresh", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await NewClient().GetAsync("/api/v1/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_ReturnsUnauthorized()
    {
        var (client, auth, _) = await SignedInClientAsync();
        var tampered = auth.AccessToken[..^4] + (auth.AccessToken.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/v1/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class AuthRateLimitTests(TestDatabase database) : ApiTestBase(database)
{
    protected override SpeakMateApiFactory CreateFactory() => new StrictRateLimitApiFactory(Database);

    [Fact]
    public async Task AuthEndpoints_BeyondPerMinuteLimit_Return429WithRetryAfter()
    {
        var client = NewClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "nobody@example.test", password = "whatever-123" }, Json, Ct);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }
}
