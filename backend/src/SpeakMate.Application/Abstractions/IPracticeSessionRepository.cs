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
}
