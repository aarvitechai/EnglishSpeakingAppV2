using Scalar.AspNetCore;
using Serilog;

namespace SpeakMate.Api.Extensions;

internal static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // Unhandled exceptions become RFC 7807 problem responses without leaking stack traces.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }
        else
        {
            app.UseHsts();
        }

        app.UseSecurityHeaders();
        app.UseHttpsRedirection();
        app.UseSerilogRequestLogging();
        app.UseCors(ServiceCollectionExtensions.WebClientCorsPolicy);

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
            await next(context);
        });
}
