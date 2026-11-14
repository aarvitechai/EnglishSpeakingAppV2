using Scalar.AspNetCore;
using Serilog;
using SpeakMate.Api.Endpoints;

namespace SpeakMate.Api.Extensions;

internal static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // Unhandled exceptions become RFC 9457 problem responses without leaking stack traces.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseSecurityHeaders();
        app.UseHttpsRedirection();
        app.UseSerilogRequestLogging();
        app.UseCors(ServiceCollectionExtensions.WebClientCorsPolicy);

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            // Interactive API reference at /scalar (development only).
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        app.MapHealthEndpoints();
        app.MapAuthEndpoints();
        app.MapMeEndpoints();
        return app;
    }

    /// <summary>Baseline security headers for a JSON API.</summary>
    private static void UseSecurityHeaders(this WebApplication app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers.CacheControl = "no-store"; // API responses may contain personal data.
            await next(context);
        });
}
