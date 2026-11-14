using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using SpeakMate.Application.Auth;
using SpeakMate.Application.Profiles;
using SpeakMate.Domain.Learners;
using SpeakMate.Infrastructure.Identity;

namespace SpeakMate.UnitTests.Application;

public sealed class ValidatorTests
{
    [Theory]
    [InlineData("learner@example.test", "long-enough-pass", "Name", true, true)]
    [InlineData("not-an-email", "long-enough-pass", "Name", true, false)]
    [InlineData("learner@example.test", "short", "Name", true, false)]
    [InlineData("learner@example.test", "long-enough-pass", "", true, false)]
    [InlineData("learner@example.test", "long-enough-pass", "Name", false, false)]
    public void RegisterValidator_EnforcesRules(string email, string password, string name, bool terms, bool valid)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest(email, password, name, terms));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void PreferencesValidator_RejectsUnsupportedLanguageAndTooManyGoals()
    {
        var result = new UpdatePreferencesRequestValidator().Validate(new UpdatePreferencesRequest(
            EnglishLevel.Beginner,
            [LearningGoal.Work, LearningGoal.Study, LearningGoal.Travel, LearningGoal.Confidence],
            "klingon",
            10));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePreferencesRequest.Goals));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePreferencesRequest.SupportLanguage));
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("hi")]
    public void PreferencesValidator_AcceptsSupportedLanguagesCaseInsensitively(string language) =>
        Assert.True(new UpdatePreferencesRequestValidator().Validate(
            new UpdatePreferencesRequest(EnglishLevel.Beginner, [LearningGoal.Travel], language, 10)).IsValid);
}

public sealed class TokenFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static TokenFactory CreateFactory() => new(
        Options.Create(new JwtOptions
        {
            SigningKey = "unit-test-signing-key-that-is-long-enough-123",
            AccessTokenMinutes = 15,
        }),
        new FakeTimeProvider(Now));

    [Fact]
    public void AccessToken_ContainsSubjectAndExpiresAfterConfiguredMinutes()
    {
        var user = new ApplicationUser { Email = "a@example.test" };

        var (token, expiresAt) = CreateFactory().CreateAccessToken(user);

        var jwt = new JsonWebToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("speakmate-api", jwt.Issuer);
        Assert.Equal(Now.AddMinutes(15), expiresAt);
        Assert.Equal("HS256", jwt.Alg);
    }

    [Fact]
    public void RefreshTokens_AreRandomAndHashedDeterministically()
    {
        var (rawA, hashA) = TokenFactory.CreateRefreshToken();
        var (rawB, _) = TokenFactory.CreateRefreshToken();

        Assert.NotEqual(rawA, rawB);
        Assert.Equal(hashA, TokenFactory.Hash(rawA));
        Assert.Equal(64, hashA.Length);
        Assert.True(rawA.Length >= 43); // 32 random bytes, Base64Url-encoded
    }

    [Fact]
    public void JwtOptions_RejectsShortSigningKey()
    {
        Assert.False(new JwtOptions { SigningKey = "too-short" }.HasStrongSigningKey);
        Assert.True(new JwtOptions { SigningKey = new string('k', JwtOptions.MinimumKeyBytes) }.HasStrongSigningKey);
    }
}
