using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace SpeakMate.Api.Endpoints;

internal static class HealthEndpoints
{
    public const string ReadyTag = SpeakMate.Infrastructure.DependencyInjection.ReadinessTag;

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness: the process is up. No dependency checks, so it never flaps on DB outages.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        // Readiness: dependencies (database, etc.) are reachable; used by load balancers.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
        });

        return app;
    }
}
