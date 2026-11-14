using System.Net;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Health;

public sealed class HealthEndpointTests(TestDatabase database) : IntegrationTestBase(database)
{
    private readonly SpeakMateApiFactory _factory = new(database);

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_WhenApiAndDatabaseAreUp_ReturnsHealthy(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task AnyResponse_IncludesSecurityHeaders()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live", Ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync("/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public override async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await base.DisposeAsync();
    }
}
