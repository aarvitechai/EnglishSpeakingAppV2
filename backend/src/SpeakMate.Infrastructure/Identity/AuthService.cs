using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeakMate.Application.Auth;
using SpeakMate.Application.Common;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Learners;
using SpeakMate.Infrastructure.Persistence;

namespace SpeakMate.Infrastructure.Identity;

internal sealed partial class AuthService(
    SpeakMateDbContext db,
    UserManager<ApplicationUser> userManager,
    TokenFactory tokenFactory,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    // One generic message for every login failure, so attackers can't tell which emails exist.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Email or password is incorrect.");

    private static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "Your session has expired. Please sign in again.");

    private static readonly Error LockedOut =
        Error.TooManyRequests("auth.locked_out", "Too many failed attempts. Try again in a few minutes.");

    public async Task<Result<AuthTokens>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = request.Email.Trim();

        // The retrying execution strategy requires user-initiated transactions to run inside it.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            var now = clock.GetUtcNow();
            var user = new ApplicationUser { UserName = email, Email = email, CreatedAt = now };

            var created = await userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                return MapIdentityErrors(created);
            }

            db.LearnerProfiles.Add(LearnerProfile.Create(user.Id, request.DisplayName, now));
            db.ConsentRecords.Add(ConsentRecord.Grant(user.Id, ConsentType.TermsAndPrivacy, Policies.CurrentVersion, now));
            var tokens = IssueTokens(user, request.DisplayName.Trim(), onboardingCompleted: false, familyId: Guid.NewGuid());

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            LogRegistered(user.Id);
            return Result<AuthTokens>.Success(tokens);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await userManager.FindByEmailAsync(request.Email.Trim()).ConfigureAwait(false);
        if (user is null)
        {
            // Run a hash anyway so response time doesn't reveal whether the email exists.
            userManager.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            return InvalidCredentials;
        }

        if (await userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            await userManager.AccessFailedAsync(user).ConfigureAwait(false);
            LogFailedLogin(user.Id);
            return await userManager.IsLockedOutAsync(user).ConfigureAwait(false) ? LockedOut : InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        var profile = await db.LearnerProfiles.AsNoTracking()
            .SingleAsync(p => p.UserId == user.Id, cancellationToken).ConfigureAwait(false);
        var tokens = IssueTokens(user, profile.DisplayName, profile.HasCompletedOnboarding, familyId: Guid.NewGuid());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tokens;
    }

    public async Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return InvalidRefreshToken;
        }

        var hash = TokenFactory.Hash(refreshToken);
        var stored = await db.Set<RefreshToken>()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();

        if (stored is null)
        {
            return InvalidRefreshToken;
        }

        if (stored.UsedAt is not null || stored.RevokedAt is not null)
        {
            // A spent token was replayed: assume it was stolen and end every session from that login.
            await RevokeFamilyAsync(stored.FamilyId, now, cancellationToken).ConfigureAwait(false);
            LogRefreshTokenReuse(stored.UserId, stored.FamilyId);
            return InvalidRefreshToken;
        }

        if (stored.ExpiresAt <= now)
        {
            return InvalidRefreshToken;
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString()).ConfigureAwait(false);
        if (user is null || await userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return InvalidRefreshToken;
        }

        var profile = await db.LearnerProfiles.AsNoTracking()
            .SingleAsync(p => p.UserId == user.Id, cancellationToken).ConfigureAwait(false);

        stored.UsedAt = now;
        var tokens = IssueTokens(user, profile.DisplayName, profile.HasCompletedOnboarding, stored.FamilyId);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated this token at the same moment; only one may win.
            return InvalidRefreshToken;
        }

        return tokens;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = TokenFactory.Hash(refreshToken);
        var familyId = await db.Set<RefreshToken>()
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (familyId is not null)
        {
            await RevokeFamilyAsync(familyId.Value, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
    }

    private AuthTokens IssueTokens(ApplicationUser user, string displayName, bool onboardingCompleted, Guid familyId)
    {
        var now = clock.GetUtcNow();
        var (accessToken, accessExpiresAt) = tokenFactory.CreateAccessToken(user);
        var (rawRefresh, refreshHash) = TokenFactory.CreateRefreshToken();
        var refreshExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays);

        db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
        });

        return new AuthTokens(accessToken, accessExpiresAt, rawRefresh, refreshExpiresAt,
            new AuthenticatedUser(user.Id, user.Email!, displayName, onboardingCompleted));
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Set<RefreshToken>()
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private static Result<AuthTokens> MapIdentityErrors(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
        {
            return Error.Conflict("auth.email_taken", "An account with this email already exists.");
        }

        var passwordErrors = result.Errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
            .Select(e => e.Description).ToArray();
        var otherErrors = result.Errors.Where(e => !e.Code.StartsWith("Password", StringComparison.Ordinal))
            .Select(e => e.Description).ToArray();

        var fields = new Dictionary<string, string[]>();
        if (passwordErrors.Length > 0)
        {
            fields["password"] = passwordErrors;
        }

        if (otherErrors.Length > 0)
        {
            fields["email"] = otherErrors;
        }

        return Error.Validation("auth.registration_invalid", "Registration details are not valid.", fields);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} registered")]
    private partial void LogRegistered(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed login for user {UserId}")]
    private partial void LogFailedLogin(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserId}; token family {FamilyId} revoked")]
    private partial void LogRefreshTokenReuse(Guid userId, Guid familyId);
}
