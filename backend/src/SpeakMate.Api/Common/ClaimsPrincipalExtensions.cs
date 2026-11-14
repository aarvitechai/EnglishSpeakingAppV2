using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SpeakMate.Api.Common;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's ID from the token's <c>sub</c> claim.</summary>
    /// <exception cref="InvalidOperationException">Called on an endpoint that does not require authentication.</exception>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("The current request has no authenticated user.");
    }
}
