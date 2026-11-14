using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SpeakMate.IntegrationTests.Infrastructure;

/// <summary>Hosts the real API in memory, wired to the test database.</summary>
public sealed class SpeakMateApiFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SpeakMateDb", database.ConnectionString);
    }
}
