using SpeakMate.Application.Practice;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Application.Abstractions;

public interface IPracticeSessionRepository
{
    /// <summary>
    /// Loads a session with its turns and feedback, scoped to its owner.
    /// Returns null both when the session doesn't exist and when it belongs to someone else,
    /// so callers can't use the response to probe for other users' IDs.
    /// </summary>
    Task<PracticeSession?> GetForUserAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(PracticeSession session);

    /// <summary>A user's sessions, newest first, as lightweight summaries (no transcript).</summary>
    Task<PagedResult<SessionSummaryDto>> ListForUserAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Practice activity (sessions with at least one learner message) since a point in time.</summary>
    Task<IReadOnlyList<PracticeActivity>> ListActivityAsync(Guid userId, DateTimeOffset since,
        CancellationToken cancellationToken = default);

    Task<int> CountCompletedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Correction categories from feedback on sessions started since a point in time.</summary>
    Task<IReadOnlyList<CorrectionCategory>> ListCorrectionCategoriesAsync(Guid userId, DateTimeOffset since,
        CancellationToken cancellationToken = default);
}

/// <summary>One practice session reduced to what progress statistics need.</summary>
public sealed record PracticeActivity(DateTimeOffset StartedAt, DateTimeOffset LastActivityAt, DateTimeOffset? EndedAt);
