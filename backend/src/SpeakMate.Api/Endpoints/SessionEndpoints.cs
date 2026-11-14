using System.Security.Claims;
using SpeakMate.Api.Common;
using SpeakMate.Api.Extensions;
using SpeakMate.Application.Practice;

namespace SpeakMate.Api.Endpoints;

internal static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sessions").WithTags("Practice sessions").RequireAuthorization();

        group.MapGet("/topics", () => TypedResults.Ok(TopicCatalog.All
                .Select(kv => new TopicSuggestionsDto(kv.Key, kv.Value))
                .ToList()))
            .WithSummary("Suggested topics for each practice mode");

        group.MapPost("/", async (StartSessionRequest request, ClaimsPrincipal user, PracticeService practice,
                    CancellationToken ct) =>
                (await practice.StartAsync(user.GetUserId(), request, ct))
                    .ToHttpResult(s => TypedResults.Created($"/api/v1/sessions/{s.Id}", s)))
            .Validate<StartSessionRequest>()
            .RequireRateLimiting(RateLimitingExtensions.AiPolicy)
            .WithSummary("Start a practice session; the coach sends the first message");

        group.MapGet("/{sessionId:guid}", async (Guid sessionId, ClaimsPrincipal user, PracticeService practice,
                    CancellationToken ct) =>
                (await practice.GetAsync(user.GetUserId(), sessionId, ct)).ToHttpResult())
            .WithSummary("Get a session with its conversation");

        group.MapPost("/{sessionId:guid}/messages", async (Guid sessionId, SendMessageRequest request,
                    ClaimsPrincipal user, PracticeService practice, CancellationToken ct) =>
                (await practice.SendMessageAsync(user.GetUserId(), sessionId, request, ct)).ToHttpResult())
            .Validate<SendMessageRequest>()
            .RequireRateLimiting(RateLimitingExtensions.AiPolicy)
            .WithSummary("Send a learner message (typed or transcribed speech) and get the coach's reply");

        return app;
    }
}
