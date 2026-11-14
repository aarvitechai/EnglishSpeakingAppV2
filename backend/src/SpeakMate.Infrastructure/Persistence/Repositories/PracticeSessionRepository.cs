using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Infrastructure.Persistence.Repositories;

internal sealed class PracticeSessionRepository(SpeakMateDbContext db) : IPracticeSessionRepository
{
    public Task<PracticeSession?> GetForUserAsync(Guid sessionId, Guid userId,
        CancellationToken cancellationToken = default) =>
        db.PracticeSessions
            .Include(s => s.Turns.OrderBy(t => t.Sequence))
            .Include(s => s.Feedback)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

    public Task<int> CountActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.PracticeSessions.CountAsync(s => s.UserId == userId && s.Status == SessionStatus.Active, cancellationToken);

    public void Add(PracticeSession session) => db.PracticeSessions.Add(session);
}
