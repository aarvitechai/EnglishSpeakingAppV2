using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SpeakMate.Api.Common;
using SpeakMate.Api.Extensions;
using SpeakMate.Application.Account;
using SpeakMate.Application.Feedback;
using SpeakMate.Application.Practice;
using SpeakMate.Application.Progress;

namespace SpeakMate.Api.Endpoints;

internal static class FeedbackAndProgressEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackAndProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/v1/sessions").WithTags("Practice sessions").RequireAuthorization();

        sessions.MapGet("/", async (ClaimsPrincipal user, ProgressService progress, CancellationToken ct,
                    int page = 1, int pageSize = 20) =>
                (await progress.ListHistoryAsync(user.GetUserId(), page, pageSize, ct)).ToHttpResult())
            .WithSummary("My practice history, newest first");

        sessions.MapPost("/{sessionId:guid}/finish", async (Guid sessionId, ClaimsPrincipal user,
                    SessionFeedbackService feedback, CancellationToken ct) =>
                (await feedback.FinishAsync(user.GetUserId(), sessionId, ct)).ToHttpResult())
            .RequireRateLimiting(RateLimitingExtensions.AiPolicy)
            .WithSummary("Finish a session and get feedback (feedback is null if the coach is unavailable; retry it)");

        sessions.MapPost("/{sessionId:guid}/feedback", async (Guid sessionId, ClaimsPrincipal user,
                    SessionFeedbackService feedback, CancellationToken ct) =>
                (await feedback.RetryFeedbackAsync(user.GetUserId(), sessionId, ct)).ToHttpResult())
            .RequireRateLimiting(RateLimitingExtensions.AiPolicy)
            .WithSummary("Retry feedback generation for a finished session");

        sessions.MapPost("/{sessionId:guid}/abandon", async (Guid sessionId, ClaimsPrincipal user,
                    SessionFeedbackService feedback, CancellationToken ct) =>
                (await feedback.AbandonAsync(user.GetUserId(), sessionId, ct)).ToHttpResult())
            .WithSummary("End a session without feedback");

        sessions.MapPut("/{sessionId:guid}/feedback/rating", async (Guid sessionId, RateFeedbackRequest request,
                    ClaimsPrincipal user, SessionFeedbackService feedback, CancellationToken ct) =>
                (await feedback.RateAsync(user.GetUserId(), sessionId, request.WasHelpful, ct)).ToHttpResult())
            .WithSummary("Tell us whether the feedback was helpful");

        app.MapGet("/api/v1/progress", async (ClaimsPrincipal user, ProgressService progress, CancellationToken ct,
                    int utcOffsetMinutes = 0) =>
                (await progress.GetAsync(user.GetUserId(), utcOffsetMinutes, ct)).ToHttpResult())
            .WithTags("Progress")
            .RequireAuthorization()
            .WithSummary("Practice minutes, streak, last 7 days and focus areas");

        var me = app.MapGroup("/api/v1/me").WithTags("Me").RequireAuthorization();

        me.MapGet("/export", async (ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
                (await accounts.ExportAsync(user.GetUserId(), ct)).ToHttpResult(export =>
                    TypedResults.File(
                        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(export, ExportJson),
                        "application/json",
                        $"speakmate-export-{export.ExportedAt:yyyyMMdd}.json")))
            .WithSummary("Download all my data as JSON");

        me.MapDelete("/", async ([FromBody] DeleteAccountRequest request, ClaimsPrincipal user, IAccountService accounts,
                    HttpContext http, CancellationToken ct) =>
            {
                var result = await accounts.DeleteAsync(user.GetUserId(), request, ct);
                if (!result.IsSuccess)
                {
                    return result.Error!.ToProblem();
                }

                http.Response.Cookies.Delete(AuthEndpoints.RefreshCookieName, new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/v1/auth",
                });
                return TypedResults.NoContent();
            })
            .Validate<DeleteAccountRequest>()
            .WithSummary("Permanently delete my account and all my data");

        return app;
    }

    private static readonly System.Text.Json.JsonSerializerOptions ExportJson = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
