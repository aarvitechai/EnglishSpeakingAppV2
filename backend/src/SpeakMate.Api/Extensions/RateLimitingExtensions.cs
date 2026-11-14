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

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);

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
