using System.Text;

namespace SpeakMate.Infrastructure.Identity;

/// <summary>JWT settings. The signing key is a secret: user-secrets locally, an environment variable or vault in production.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "speakmate-api";
    public string Audience { get; set; } = "speakmate-clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;

    public bool HasStrongSigningKey => Encoding.UTF8.GetByteCount(SigningKey) >= MinimumKeyBytes;
}
