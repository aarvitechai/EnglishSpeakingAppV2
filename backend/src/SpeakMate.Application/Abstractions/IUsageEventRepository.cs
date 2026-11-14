using SpeakMate.Domain.Usage;

namespace SpeakMate.Application.Abstractions;

public interface IUsageEventRepository
{
    void Add(UsageEvent usageEvent);

    /// <summary>Total tokens a user consumed since <paramref name="since"/>; drives daily quotas.</summary>
    Task<int> SumTokensForUserSinceAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default);
}
