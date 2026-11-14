using FluentValidation;
using SpeakMate.Application.Common;
using SpeakMate.Domain.Learners;

namespace SpeakMate.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, bool AcceptTerms);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedUser(Guid Id, string Email, string DisplayName, bool OnboardingCompleted);

/// <summary>
/// Issued tokens. The refresh token is returned only so the API layer can put it in an
/// HttpOnly cookie; it is never serialised into a response body.
/// </summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUser User);

/// <summary>Account use cases. Implemented in Infrastructure because it relies on ASP.NET Core Identity.</summary>
public interface IAuthService
{
    Task<Result<AuthTokens>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Exchanges a refresh token for new tokens (rotation). Reuse of a spent token revokes the whole family.</summary>
    Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public static class AuthRules
{
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;
    public const int EmailMaxLength = 254;
}

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(AuthRules.EmailMaxLength);
        RuleFor(r => r.Password).NotEmpty()
            .MinimumLength(AuthRules.PasswordMinLength)
            .MaximumLength(AuthRules.PasswordMaxLength);
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(LearnerProfile.DisplayNameMaxLength);
        RuleFor(r => r.AcceptTerms).Equal(true).WithMessage("You must accept the terms and privacy policy.");
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(AuthRules.EmailMaxLength);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(AuthRules.PasswordMaxLength);
    }
}
