namespace SpeakMate.Application.Abstractions;

/// <summary>Commits all tracked changes as one transaction.</summary>
public interface IUnitOfWork
{
    /// <exception cref="ConcurrencyConflictException">Another request changed the same data first.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised when a write loses a race: an optimistic-concurrency check failed, or another request
/// inserted a conflicting unique row first (e.g. two tabs posting to one session). Maps to HTTP 409.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException() : base("The data was changed by another request.") { }

    public ConcurrencyConflictException(string message) : base(message) { }

    public ConcurrencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}
