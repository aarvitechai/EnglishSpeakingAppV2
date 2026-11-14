using FluentValidation;
using SpeakMate.Domain.Learners;

namespace SpeakMate.Application.Profiles;

public sealed record ProfileDto(
    string DisplayName,
    EnglishLevel Level,
    IReadOnlyList<LearningGoal> Goals,
    string SupportLanguage,
    int DailyTargetMinutes,
    bool OnboardingCompleted);

public sealed record UpdatePreferencesRequest(
    EnglishLevel Level,
    IReadOnlyList<LearningGoal> Goals,
    string SupportLanguage,
    int DailyTargetMinutes);

public sealed record RenameRequest(string DisplayName);

/// <summary>Languages the coach can use for short explanations (BCP 47 primary tags).</summary>
public static class SupportLanguages
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "en", "hi", "bn", "ta", "te", "mr", "ur", "es", "pt", "fr", "de", "it", "ar", "tr", "ru",
        "zh", "ja", "ko", "id", "vi", "th", "fa", "pl", "uk",
    };
}

internal sealed class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesRequestValidator()
    {
        RuleFor(r => r.Level).IsInEnum();
        RuleFor(r => r.Goals).NotNull()
            .Must(g => g.Count is > 0 and <= LearnerProfile.MaxGoals)
            .WithMessage($"Choose between 1 and {LearnerProfile.MaxGoals} goals.");
        RuleForEach(r => r.Goals).IsInEnum();
        RuleFor(r => r.SupportLanguage).NotEmpty()
            .Must(SupportLanguages.All.Contains).WithMessage("This support language is not available yet.");
        RuleFor(r => r.DailyTargetMinutes)
            .InclusiveBetween(LearnerProfile.MinDailyTargetMinutes, LearnerProfile.MaxDailyTargetMinutes);
    }
}

internal sealed class RenameRequestValidator : AbstractValidator<RenameRequest>
{
    public RenameRequestValidator() =>
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(LearnerProfile.DisplayNameMaxLength);
}
