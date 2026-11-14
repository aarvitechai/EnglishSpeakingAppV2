using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;
using SpeakMate.Domain.Consent;

namespace SpeakMate.Application.Consents;

public sealed record ConsentStatusDto(ConsentType Type, bool Granted, string? PolicyVersion, DateTimeOffset? UpdatedAt,
    bool Required);

public sealed record SetConsentRequest(bool Granted);

/// <summary>Reads and changes a user's consent decisions. Every change appends history; nothing is overwritten.</summary>
public sealed class ConsentService(IConsentRepository consents, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<IReadOnlyList<ConsentStatusDto>> GetStatusAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var records = await consents.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Enum.GetValues<ConsentType>().Select(type => ToStatus(type, records)).ToList();
    }

    public async Task<bool> HasActiveConsentAsync(Guid userId, ConsentType type,
        CancellationToken cancellationToken = default)
    {
        var records = await consents.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Latest(type, records)?.IsActive ?? false;
    }

    public async Task<Result<ConsentStatusDto>> SetAsync(Guid userId, ConsentType type, bool granted,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type))
        {
            return Error.NotFound("consent.unknown_type", "Unknown consent type.");
        }

        if (type == ConsentType.TermsAndPrivacy && !granted)
        {
            return Error.Validation("consent.required",
                "Terms consent can't be withdrawn. Delete your account instead.");
        }

        var records = await consents.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var current = Latest(type, records);
        var now = clock.GetUtcNow();

        if (granted && current?.IsActive != true)
        {
            var record = ConsentRecord.Grant(userId, type, Policies.CurrentVersion, now);
            consents.Add(record);
            records = [.. records, record];
        }
        else if (!granted && current?.IsActive == true)
        {
            current.Withdraw(now);
        }
        else
        {
            return ToStatus(type, records); // Already in the requested state: idempotent no-op.
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToStatus(type, records);
    }

    private static ConsentRecord? Latest(ConsentType type, IEnumerable<ConsentRecord> records) =>
        records.Where(r => r.Type == type).MaxBy(r => r.GrantedAt);

    private static ConsentStatusDto ToStatus(ConsentType type, IReadOnlyList<ConsentRecord> records)
    {
        var latest = Latest(type, records);
        return new ConsentStatusDto(type, latest?.IsActive ?? false, latest?.PolicyVersion,
            latest?.WithdrawnAt ?? latest?.GrantedAt, Required: type == ConsentType.TermsAndPrivacy);
    }
}
