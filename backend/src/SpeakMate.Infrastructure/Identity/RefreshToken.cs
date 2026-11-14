namespace SpeakMate.Infrastructure.Identity;

/// <summary>
/// A single-use refresh token. Only a SHA-256 hash is stored, so a database leak does not expose
/// usable tokens. Tokens issued from one login share a <see cref="FamilyId"/>: if an already-used
/// token is presented again (likely theft), the whole family is revoked.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }

    /// <summary>Lowercase hex SHA-256 of the raw token.</summary>
    public required string TokenHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && RevokedAt is null && ExpiresAt > now;
}
