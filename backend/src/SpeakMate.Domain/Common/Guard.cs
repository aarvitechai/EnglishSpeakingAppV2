namespace SpeakMate.Domain.Common;

internal static class Guard
{
    public static string NotBlank(string? value, string code, string message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(code, message);
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength
            ? throw new DomainException(code, $"Value must be at most {maxLength} characters.")
            : trimmed;
    }

    public static Guid NotEmpty(Guid value, string code) =>
        value == Guid.Empty ? throw new DomainException(code, "Identifier must not be empty.") : value;
}
