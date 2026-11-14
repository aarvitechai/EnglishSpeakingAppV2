using Microsoft.Extensions.DependencyInjection;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Usage;

namespace SpeakMate.Infrastructure.Persistence.Repositories;

/// <summary>
/// Saves a usage record through a brand-new DbContext, so nothing else tracked by the
/// current request (such as an unsaved learner turn) is persisted alongside it.
/// </summary>
internal sealed class IsolatedUsageRecorder(IServiceScopeFactory scopeFactory) : IUsageRecorder
{
    public async Task SaveIsolatedAsync(UsageEvent usageEvent, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakMateDbContext>();
        db.UsageEvents.Add(usageEvent);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
