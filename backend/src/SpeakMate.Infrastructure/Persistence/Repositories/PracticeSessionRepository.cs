using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Practice;
using SpeakMate.Domain.Feedback;
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

    public async Task<PagedResult<SessionSummaryDto>> ListForUserAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = db.PracticeSessions.AsNoTracking().Where(s => s.UserId == userId);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SessionSummaryDto(
                s.Id,
                s.Mode,
                s.Topic,
                s.Status,
                s.StartedAt,
                s.EndedAt,
                EF.Functions.DateDiffSecond(s.StartedAt, s.EndedAt ?? s.LastActivityAt),
                s.LearnerMessageCount,
                s.Feedback != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<SessionSummaryDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<PracticeActivity>> ListActivityAsync(Guid userId, DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await db.PracticeSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.StartedAt >= since && s.LearnerMessageCount > 0)
            .Select(s => new PracticeActivity(s.StartedAt, s.LastActivityAt, s.EndedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountCompletedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.PracticeSessions.CountAsync(s => s.UserId == userId && s.Status == SessionStatus.Completed, cancellationToken);

    public async Task<IReadOnlyList<CorrectionCategory>> ListCorrectionCategoriesAsync(Guid userId, DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var feedback = await db.SessionFeedback.AsNoTracking()
            .Where(f => db.PracticeSessions.Any(s => s.Id == f.SessionId && s.UserId == userId && s.StartedAt >= since))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return feedback.SelectMany(f => f.Corrections).Select(c => c.Category).ToList();
    }
}
