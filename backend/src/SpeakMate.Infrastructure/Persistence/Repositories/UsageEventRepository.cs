using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Usage;

namespace SpeakMate.Infrastructure.Persistence.Repositories;

internal sealed class UsageEventRepository(SpeakMateDbContext db) : IUsageEventRepository
{
    public void Add(UsageEvent usageEvent) => db.UsageEvents.Add(usageEvent);

    public async Task<int> SumTokensForUserSinceAsync(Guid userId, DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await db.UsageEvents
            .Where(u => u.UserId == userId && u.CreatedAt >= since)
            .SumAsync(u => (int?)(u.InputTokens + u.OutputTokens), cancellationToken)
            .ConfigureAwait(false) ?? 0;
}
