using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakMate.Application.Account;
using SpeakMate.Application.Common;
using SpeakMate.Application.Practice;
using SpeakMate.Application.Profiles;
using SpeakMate.Infrastructure.Persistence;

namespace SpeakMate.Infrastructure.Identity;

internal sealed partial class AccountService(
    SpeakMateDbContext db,
    UserManager<ApplicationUser> userManager,
    TimeProvider clock,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<Result> DeleteAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Error.NotFound("account.not_found", "Account was not found.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            await userManager.AccessFailedAsync(user).ConfigureAwait(false);
            return Error.Validation("account.password_incorrect", "Password is incorrect.",
                new Dictionary<string, string[]> { ["password"] = ["Password is incorrect."] });
        }

        // Every learner table cascades from auth.Users, so this removes all personal data in one statement.
        var result = await userManager.DeleteAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Error.Unavailable("account.delete_failed", "Your account could not be deleted. Please try again.");
        }

        LogAccountDeleted(userId);
        return Result.Success();
    }

    public async Task<Result<AccountExportDto>> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        var profile = await db.LearnerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null || profile is null)
        {
            return Error.NotFound("account.not_found", "Account was not found.");
        }

        var consents = await db.ConsentRecords.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.GrantedAt)
            .Select(c => new ConsentHistoryDto(c.Type.ToString(), c.PolicyVersion, c.GrantedAt, c.WithdrawnAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var sessions = await db.PracticeSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Include(s => s.Turns)
            .Include(s => s.Feedback)
            .AsSplitQuery()
            .OrderBy(s => s.StartedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AccountExportDto(
            clock.GetUtcNow(),
            user.Email ?? string.Empty,
            user.CreatedAt,
            ProfileService.ToDto(profile),
            consents,
            sessions.Select(SessionMapping.ToDto).ToList());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Account {UserId} deleted at the user's request")]
    private partial void LogAccountDeleted(Guid userId);
}
