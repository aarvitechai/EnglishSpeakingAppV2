using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;

namespace SpeakMate.Application.Usage;

/// <summary>Cost-control limits. Bound from the "Usage" configuration section.</summary>
public sealed class UsageLimits
{
    public const string SectionName = "Usage";

    /// <summary>Rolling 24-hour token allowance per learner (input + output).</summary>
    public int DailyTokenLimitPerUser { get; set; } = 150_000;

    /// <summary>How many unfinished sessions a learner may have open at once.</summary>
    public int MaxActiveSessionsPerUser { get; set; } = 3;

    /// <summary>Price per one million tokens, keyed by model ID, for cost estimates.</summary>
    public Dictionary<string, ModelPrice> Pricing { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModelPrice
{
    public decimal InputPerMillionUsd { get; set; }
    public decimal OutputPerMillionUsd { get; set; }
}

/// <summary>Enforces per-user quotas and estimates the cost of each AI call.</summary>
public sealed class UsageGuard(IUsageEventRepository usage, UsageLimits limits, TimeProvider clock)
{
    internal static readonly Error QuotaExceeded = Error.TooManyRequests("usage.daily_limit",
        "You've reached today's practice limit. It resets gradually over the next 24 hours.");

    public async Task<Error?> CheckDailyQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var since = clock.GetUtcNow().AddHours(-24);
        var used = await usage.SumTokensForUserSinceAsync(userId, since, cancellationToken).ConfigureAwait(false);
        return used >= limits.DailyTokenLimitPerUser ? QuotaExceeded : null;
    }

    public decimal EstimateCostUsd(AiUsage aiUsage)
    {
        ArgumentNullException.ThrowIfNull(aiUsage);
        if (!limits.Pricing.TryGetValue(aiUsage.Model, out var price))
        {
            return 0m; // Unknown model: record tokens anyway; cost reports show it as unpriced.
        }

        return (aiUsage.InputTokens * price.InputPerMillionUsd + aiUsage.OutputTokens * price.OutputPerMillionUsd)
               / 1_000_000m;
    }
}
