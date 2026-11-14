namespace SpeakMate.Infrastructure.Ai;

public enum AiProvider
{
    /// <summary>Gemini when an API key is configured, otherwise the offline fake (development convenience).</summary>
    Auto = 0,
    Gemini = 1,
    Fake = 2,
}

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.Gemini;
}

/// <summary>Gemini settings. <see cref="ApiKey"/> is a secret: user-secrets locally, environment/vault in production.</summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model ID from Google's current model list. Kept in config so it can change without a code change.</summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    public Uri BaseUrl { get; set; } = new("https://generativelanguage.googleapis.com/");

    /// <summary>Thinking effort for Gemini 3+ models (MINIMAL, LOW, MEDIUM, HIGH). Empty to omit.</summary>
    public string ThinkingLevel { get; set; } = "LOW";

    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>Per-attempt timeout; the resilience pipeline retries transient failures within a total budget.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 20;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
