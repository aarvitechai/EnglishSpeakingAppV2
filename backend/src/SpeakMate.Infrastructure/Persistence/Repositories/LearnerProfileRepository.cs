using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Learners;

namespace SpeakMate.Infrastructure.Persistence.Repositories;

internal sealed class LearnerProfileRepository(SpeakMateDbContext db) : ILearnerProfileRepository
{
    public Task<LearnerProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.LearnerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public void Add(LearnerProfile profile) => db.LearnerProfiles.Add(profile);
}
