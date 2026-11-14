using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SpeakMate.Api.Common;
using SpeakMate.Application.Common;

namespace SpeakMate.Api.Extensions;

internal static class RateLimitingExtensions
{
    /// <summary>Strict per-IP limit for sign-in and registration (slows password guessing).</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Per-user limit on AI-backed endpoints (bounds cost if a client misbehaves).</summary>
    public const string AiPolicy = "ai";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
        var aiPermitLimit = configuration.GetValue("RateLimiting:AiPermitsPerMinute", 20);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AuthPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.AddPolicy(AiPolicy, http => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: http.User.FindFirst("sub")?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = aiPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problem = Error.TooManyRequests("rate_limited", "Too many requests. Please wait a moment and try again.")
                    .ToProblem();
                await problem.ExecuteAsync(context.HttpContext).ConfigureAwait(false);
            };
        });

        return services;
    }
}
