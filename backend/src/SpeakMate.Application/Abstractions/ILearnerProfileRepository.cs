using SpeakMate.Domain.Learners;

namespace SpeakMate.Application.Abstractions;

public interface ILearnerProfileRepository
{
    Task<LearnerProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(LearnerProfile profile);
}
