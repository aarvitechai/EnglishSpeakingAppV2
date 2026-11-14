using FluentValidation;
using SpeakMate.Application.Common;
using SpeakMate.Application.Practice;
using SpeakMate.Application.Profiles;

namespace SpeakMate.Application.Account;

/// <summary>Deletion requires the password again, so a stolen access token alone cannot erase an account.</summary>
public sealed record DeleteAccountRequest(string Password);

/// <summary>Everything stored about the learner, in a portable format (right of access / portability).</summary>
public sealed record AccountExportDto(
    DateTimeOffset ExportedAt,
    string Email,
    DateTimeOffset AccountCreatedAt,
    ProfileDto Profile,
    IReadOnlyList<ConsentHistoryDto> Consents,
    IReadOnlyList<SessionDto> Sessions);

public sealed record ConsentHistoryDto(string Type, string PolicyVersion, DateTimeOffset GrantedAt, DateTimeOffset? WithdrawnAt);

public interface IAccountService
{
    /// <summary>Permanently deletes the account and, through cascades, all learner data.</summary>
    Task<Result> DeleteAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken = default);

    Task<Result<AccountExportDto>> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
}

internal sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator() => RuleFor(r => r.Password).NotEmpty();
}
