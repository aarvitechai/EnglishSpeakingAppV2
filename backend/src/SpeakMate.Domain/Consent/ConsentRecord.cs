using SpeakMate.Domain.Common;

namespace SpeakMate.Domain.Consent;

public enum ConsentType
{
    /// <summary>Required to use the service: terms of use and privacy policy.</summary>
    TermsAndPrivacy = 1,

    /// <summary>Optional: keep conversation transcripts after a session ends.</summary>
    TranscriptStorage = 2,

    /// <summary>Optional: use anonymised usage to improve the product.</summary>
    ProductImprovement = 3,
}

/// <summary>
/// An append-only record of one consent decision. Withdrawal is recorded, never deleted,
/// so there is an audit trail of what the user agreed to and when.
/// </summary>
public sealed class ConsentRecord : Entity
{
    public const int PolicyVersionMaxLength = 20;

    private ConsentRecord() { } // EF Core

    private ConsentRecord(Guid userId, ConsentType type, string policyVersion, DateTimeOffset now)
    {
        UserId = Guard.NotEmpty(userId, "consent.user_required");
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("consent.type_invalid", "Unknown consent type.");
        }

        Type = type;
        PolicyVersion = Guard.NotBlank(policyVersion, "consent.policy_version_required",
            "Policy version is required.", PolicyVersionMaxLength);
        GrantedAt = now;
    }

    public Guid UserId { get; private set; }
    public ConsentType Type { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }

    public bool IsActive => WithdrawnAt is null;

    public static ConsentRecord Grant(Guid userId, ConsentType type, string policyVersion, DateTimeOffset now) =>
        new(userId, type, policyVersion, now);

    public void Withdraw(DateTimeOffset now)
    {
        if (Type == ConsentType.TermsAndPrivacy)
        {
            throw new DomainException("consent.required",
                "Terms consent cannot be withdrawn; delete the account instead.");
        }

        WithdrawnAt ??= now;
    }
}
