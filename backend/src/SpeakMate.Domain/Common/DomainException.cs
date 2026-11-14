namespace SpeakMate.Domain.Common;

/// <summary>
/// Thrown when an operation would break a business invariant.
/// Represents a programming or flow error, not normal validation (that is handled before the domain).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message) : base(message) => Code = code;

    public DomainException() : this("domain.error", "A domain rule was violated.") { }

    public DomainException(string message) : this("domain.error", message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) =>
        Code = "domain.error";

    /// <summary>Stable, machine-readable error code (e.g. "session.not_active").</summary>
    public string Code { get; }
}
