using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using SpeakMate.IntegrationTests.Infrastructure;

namespace SpeakMate.IntegrationTests.Health;

public sealed class HardeningTests(TestDatabase database) : ApiTestBase(database)
{
    [Fact]
    public async Task ProblemResponses_IncludeTraceIdAndCode()
    {
        var response = await NewClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "nobody@example.test", password = "wrong-password-1" }, Json, Ct);

        var problem = await ReadJsonAsync(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.Equal("auth.invalid_credentials", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApiResponses_AreNotCached()
    {
        var (client, _, _) = await SignedInClientAsync();

        var response = await client.GetAsync("/api/v1/me", Ct);

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task MalformedJson_Returns400NotServerError()
    {
        var (client, _, _) = await SignedInClientAsync();
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/api/v1/me/display-name", content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class GlobalRateLimitTests(TestDatabase database) : ApiTestBase(database)
{
    protected override SpeakMateApiFactory CreateFactory() => new StrictGlobalLimitApiFactory(Database);

    [Fact]
    public async Task EveryEndpoint_HasABaselinePerClientLimit_ButHealthProbesAreExempt()
    {
        var client = NewClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            statuses.Add((await client.GetAsync("/api/v1/sessions/topics", Ct)).StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", Ct)).StatusCode);
        }
    }
}

public sealed class ProductionConfigurationTests(TestDatabase database) : IntegrationTestBase(database)
{
    private sealed class ProductionFactory(TestDatabase db, string corsOrigin, string connectionString)
        : SpeakMateApiFactory(db)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.UseSetting("Cors:AllowedOrigins:0", corsOrigin);
            builder.UseSetting("ConnectionStrings:SpeakMateDb", connectionString);
        }
    }

    [Theory]
    [InlineData("http://insecure.example", "Server=db;Database=x;Encrypt=True", "https://")]
    [InlineData("*", "Server=db;Database=x;Encrypt=True", "https://")]
    [InlineData("https://app.example", "Server=db;Database=x;TrustServerCertificate=True", "TrustServerCertificate")]
    public async Task UnsafeProductionSettings_PreventStartup(string origin, string connection, string expected)
    {
        await using var factory = new ProductionFactory(Database, origin, connection);

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var message = ex is AggregateException agg ? agg.Flatten().InnerException?.Message ?? ex.Message : ex.Message;
        Assert.Contains(expected, message, StringComparison.Ordinal);
    }
}
