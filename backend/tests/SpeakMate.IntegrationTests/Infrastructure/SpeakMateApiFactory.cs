using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SpeakMate.IntegrationTests.Infrastructure;

/// <summary>Hosts the real API in memory, wired to the test database with test-only secrets.</summary>
public class SpeakMateApiFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    /// <summary>Test-only signing key. Never used outside the in-memory test host.</summary>
    public const string TestSigningKey = "integration-tests-only-signing-key-0123456789abcdef";

    /// <summary>Raised far above normal so ordinary tests never trip the auth rate limiter.</summary>
    protected virtual int AuthPermitsPerMinute => 10_000;

    /// <summary>Extra configuration values for a specific test (e.g. a tiny quota).</summary>
    public IReadOnlyDictionary<string, string>? Settings { get; init; }

    /// <summary>Lets a test replace services (e.g. a fake AI client or a fake clock).</summary>
    public Action<IServiceCollection>? ConfigureTestServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SpeakMateDb", database.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Ai:Provider", "Fake");
        builder.UseSetting("Retention:Enabled", "false");
        builder.UseSetting("RateLimiting:AiPermitsPerMinute", "10000");
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", AuthPermitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));

        foreach (var (key, value) in Settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        if (ConfigureTestServices is not null)
        {
            builder.ConfigureTestServices(ConfigureTestServices);
        }
    }

    /// <summary>HTTPS client that keeps cookies, so the Secure refresh cookie round-trips like in a browser.</summary>
    public HttpClient CreateBrowserLikeClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
}

/// <summary>Factory with a tiny auth rate limit, for testing the limiter itself.</summary>
public sealed class StrictRateLimitApiFactory(TestDatabase database) : SpeakMateApiFactory(database)
{
    protected override int AuthPermitsPerMinute => 3;
}
