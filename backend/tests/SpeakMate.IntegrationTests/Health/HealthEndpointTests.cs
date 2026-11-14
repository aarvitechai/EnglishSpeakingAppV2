using System.Net;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Health;

public sealed class HealthEndpointTests(TestDatabase database) : ApiTestBase(database)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_WhenApiAndDatabaseAreUp_ReturnsHealthy(string url)
    {
        var response = await NewClient().GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task AnyResponse_IncludesSecurityHeaders()
    {
        var response = await NewClient().GetAsync("/health/live", Ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task UnknownRoute_Anonymous_ReturnsUnauthorized()
    {
        // Secure-by-default fallback policy: anonymous callers can't probe which routes exist.
        var response = await NewClient().GetAsync("/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_SignedIn_ReturnsNotFound()
    {
        var (client, _, _) = await SignedInClientAsync();

        var response = await client.GetAsync("/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
