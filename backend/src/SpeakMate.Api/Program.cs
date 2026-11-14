using System.Globalization;
using Serilog;
using SpeakMate.Api.Extensions;

// Bootstrap logger captures failures that happen before the host is fully built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

    builder.AddHardening();
    builder.Services.AddApiServices(builder.Configuration);

    var app = builder.Build();

    var ai = app.Services.GetRequiredService<SpeakMate.Infrastructure.AiRuntimeInfo>();
    Log.Information("AI provider: {Provider}", ai.Provider);

    app.UseApiPipeline();
    app.MapApiEndpoints();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown by design when EF tooling builds the host; not a crash.
    Log.Fatal(ex, "SpeakMate API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry point marker, exposed so integration tests can host the API in memory.</summary>
public partial class Program;
