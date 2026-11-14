using SpeakMate.Domain.Consent;

namespace SpeakMate.Application.Abstractions;

public interface IConsentRepository
{
    Task<IReadOnlyList<ConsentRecord>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(ConsentRecord record);
}
