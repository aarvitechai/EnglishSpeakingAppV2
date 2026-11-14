namespace SpeakMate.Application.Common;

public enum ErrorType
{
    Validation = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    Conflict = 5,
    TooManyRequests = 6,
    Unavailable = 7,
}

/// <summary>An expected, user-facing failure. Unexpected failures stay exceptions.</summary>
/// <param name="Code">Stable machine-readable code, e.g. "auth.invalid_credentials".</param>
/// <param name="Message">Safe, human-readable message; never contains internals.</param>
/// <param name="Type">Category that decides the HTTP status code.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Field-level validation messages, keyed by property name.</summary>
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? fields = null) =>
        new(code, message, ErrorType.Validation) { FieldErrors = fields };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error TooManyRequests(string code, string message) => new(code, message, ErrorType.TooManyRequests);
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}

/// <summary>Success-or-error outcome of a use case.</summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    /// <exception cref="InvalidOperationException">Accessed on a failed result.</exception>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
