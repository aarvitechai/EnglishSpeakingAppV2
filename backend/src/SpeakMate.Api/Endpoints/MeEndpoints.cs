using System.Security.Claims;
using SpeakMate.Api.Common;
using SpeakMate.Application.Consents;
using SpeakMate.Application.Profiles;
using SpeakMate.Domain.Consent;

namespace SpeakMate.Api.Endpoints;

/// <summary>The signed-in learner's own profile and consent settings. User ID always comes from the token, never the URL.</summary>
internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/me").WithTags("Me").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, ProfileService profiles, CancellationToken ct) =>
                (await profiles.GetAsync(user.GetUserId(), ct)).ToHttpResult())
            .WithSummary("Get my learner profile");

        group.MapPut("/onboarding", async (UpdatePreferencesRequest request, ClaimsPrincipal user,
                    ProfileService profiles, CancellationToken ct) =>
                (await profiles.CompleteOnboardingAsync(user.GetUserId(), request, ct)).ToHttpResult())
            .Validate<UpdatePreferencesRequest>()
            .WithSummary("Complete onboarding: level, goals, support language, daily target");

        group.MapPut("/preferences", async (UpdatePreferencesRequest request, ClaimsPrincipal user,
                    ProfileService profiles, CancellationToken ct) =>
                (await profiles.UpdatePreferencesAsync(user.GetUserId(), request, ct)).ToHttpResult())
            .Validate<UpdatePreferencesRequest>()
            .WithSummary("Update learning preferences");

        group.MapPut("/display-name", async (RenameRequest request, ClaimsPrincipal user,
                    ProfileService profiles, CancellationToken ct) =>
                (await profiles.RenameAsync(user.GetUserId(), request, ct)).ToHttpResult())
            .Validate<RenameRequest>()
            .WithSummary("Change my display name");

        group.MapGet("/consents", async (ClaimsPrincipal user, ConsentService consents, CancellationToken ct) =>
                TypedResults.Ok(await consents.GetStatusAsync(user.GetUserId(), ct)))
            .WithSummary("List my consent decisions");

        group.MapPut("/consents/{type}", async (ConsentType type, SetConsentRequest request, ClaimsPrincipal user,
                    ConsentService consents, CancellationToken ct) =>
                (await consents.SetAsync(user.GetUserId(), type, request.Granted, ct)).ToHttpResult())
            .WithSummary("Grant or withdraw an optional consent");

        return app;
    }
}
