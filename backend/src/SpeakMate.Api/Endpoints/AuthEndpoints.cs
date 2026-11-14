using SpeakMate.Api.Common;
using SpeakMate.Api.Extensions;
using SpeakMate.Application.Auth;

namespace SpeakMate.Api.Endpoints;

/// <summary>Access token in the response body (kept in memory by the client); refresh token only in an HttpOnly cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthenticatedUser User);

internal static class AuthEndpoints
{
    public const string RefreshCookieName = "speakmate_refresh";
    private const string CookiePath = "/api/v1/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/register", async (RegisterRequest request, IAuthService auth, HttpContext http,
                CancellationToken ct) =>
            (await auth.RegisterAsync(request, ct)).ToHttpResult(tokens =>
            {
                SetRefreshCookie(http, tokens);
                return TypedResults.Created("/api/v1/me", ToResponse(tokens));
            }))
            .Validate<RegisterRequest>()
            .WithSummary("Create an account and sign in");

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, HttpContext http,
                CancellationToken ct) =>
            (await auth.LoginAsync(request, ct)).ToHttpResult(tokens =>
            {
                SetRefreshCookie(http, tokens);
                return TypedResults.Ok(ToResponse(tokens));
            }))
            .Validate<LoginRequest>()
            .WithSummary("Sign in with email and password");

        group.MapPost("/refresh", async (IAuthService auth, HttpContext http, CancellationToken ct) =>
            {
                var token = http.Request.Cookies[RefreshCookieName] ?? string.Empty;
                var result = await auth.RefreshAsync(token, ct);
                if (!result.IsSuccess)
                {
                    ClearRefreshCookie(http);
                }

                return result.ToHttpResult(tokens =>
                {
                    SetRefreshCookie(http, tokens);
                    return TypedResults.Ok(ToResponse(tokens));
                });
            })
            .WithSummary("Get a new access token using the refresh cookie");

        group.MapPost("/logout", async (IAuthService auth, HttpContext http, CancellationToken ct) =>
            {
                await auth.LogoutAsync(http.Request.Cookies[RefreshCookieName] ?? string.Empty, ct);
                ClearRefreshCookie(http);
                return TypedResults.NoContent();
            })
            .WithSummary("Sign out and revoke the refresh token");

        return app;
    }

    private static AuthResponse ToResponse(AuthTokens tokens) =>
        new(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.User);

    private static void SetRefreshCookie(HttpContext http, AuthTokens tokens) =>
        http.Response.Cookies.Append(RefreshCookieName, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,                   // JavaScript cannot read it (XSS can't steal it).
            Secure = true,                     // HTTPS only.
            SameSite = SameSiteMode.Strict,    // Not sent on cross-site requests (CSRF protection).
            Path = CookiePath,                 // Only sent to the auth endpoints.
            Expires = tokens.RefreshTokenExpiresAt,
            IsEssential = true,
        });

    private static void ClearRefreshCookie(HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
        });
}
