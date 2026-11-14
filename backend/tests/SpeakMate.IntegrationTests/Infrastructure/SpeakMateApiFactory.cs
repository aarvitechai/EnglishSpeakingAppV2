using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SpeakMate.IntegrationTests.Infrastructure;

/// <summary>Hosts the real API in memory for end-to-end HTTP tests.</summary>
public sealed class SpeakMateApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
