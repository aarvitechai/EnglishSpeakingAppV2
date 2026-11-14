using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;

namespace SpeakMate.Api.Extensions;

/// <summary>Production safety settings: proxy trust, request limits, diagnostics and config validation.</summary>
internal static class HardeningExtensions
{
    /// <summary>JSON API requests are small; anything larger is rejected before it is buffered.</summary>
    public const long MaxRequestBodyBytes = 64 * 1024;

    public static WebApplicationBuilder AddHardening(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false; // don't advertise the server software
            kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
            kestrel.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
            kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });

        // Behind a reverse proxy, trust X-Forwarded-For/Proto ONLY from configured proxy addresses.
        // Otherwise any client could spoof its IP and bypass per-IP rate limits.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }
        });

        // Every problem response carries the trace id, so a user-reported error can be matched to server logs.
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] =
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        ValidateProductionConfiguration(builder);
        return builder;
    }

    public static WebApplication UseForwardedHeadersFromTrustedProxies(this WebApplication app)
    {
        app.UseForwardedHeaders();
        return app;
    }

    /// <summary>Request logging without health-probe noise. Never logs bodies, headers or query strings.</summary>
    public static WebApplication UseQuietRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";
            options.GetLevel = (http, _, exception) =>
                exception is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
                : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });
        return app;
    }

    /// <summary>Fail fast on unsafe production settings instead of discovering them in an incident.</summary>
    private static void ValidateProductionConfiguration(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsProduction())
        {
            return;
        }

        var problems = new List<string>();
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(o => o == "*" || !o.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add("Cors:AllowedOrigins must list explicit https:// origins in production.");
        }

        var connection = builder.Configuration.GetConnectionString(Infrastructure.DependencyInjection.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connection))
        {
            problems.Add("ConnectionStrings:SpeakMateDb is required in production.");
        }
        else if (connection.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("Production database connections must validate the server certificate (remove TrustServerCertificate=True).");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Unsafe production configuration:\n - " + string.Join("\n - ", problems));
        }
    }
}
