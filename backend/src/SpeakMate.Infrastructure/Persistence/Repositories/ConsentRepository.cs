using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Consent;

namespace SpeakMate.Infrastructure.Persistence.Repositories;

internal sealed class ConsentRepository(SpeakMateDbContext db) : IConsentRepository
{
    public async Task<IReadOnlyList<ConsentRecord>> ListForUserAsync(Guid userId,
        CancellationToken cancellationToken = default) =>
        await db.ConsentRecords
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.GrantedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(ConsentRecord record) => db.ConsentRecords.Add(record);
}
