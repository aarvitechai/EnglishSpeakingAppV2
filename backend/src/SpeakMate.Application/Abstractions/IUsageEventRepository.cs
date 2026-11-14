using SpeakMate.Domain.Usage;

namespace SpeakMate.Application.Abstractions;

public interface IUsageEventRepository
{
    void Add(UsageEvent usageEvent);

    /// <summary>Total tokens a user consumed since <paramref name="since"/>; drives daily quotas.</summary>
    Task<int> SumTokensForUserSinceAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default);
}

/// <summary>Persists a usage record independently of the current unit of work (used when a request fails).</summary>
public interface IUsageRecorder
{
    Task SaveIsolatedAsync(UsageEvent usageEvent, CancellationToken cancellationToken = default);
}
