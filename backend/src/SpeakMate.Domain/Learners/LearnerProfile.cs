using SpeakMate.Domain.Common;

namespace SpeakMate.Domain.Learners;

/// <summary>A learner's preferences. One profile per user account.</summary>
public sealed class LearnerProfile : Entity
{
    public const int DisplayNameMaxLength = 80;
    public const int SupportLanguageMaxLength = 35;
    public const int MinDailyTargetMinutes = 5;
    public const int MaxDailyTargetMinutes = 120;
    public const int MaxGoals = 3;

    private List<LearningGoal> _goals = [];

    private LearnerProfile() { } // EF Core

    private LearnerProfile(Guid userId, string displayName, DateTimeOffset now)
    {
        UserId = Guard.NotEmpty(userId, "profile.user_required");
        DisplayName = Guard.NotBlank(displayName, "profile.display_name_required",
            "Display name is required.", DisplayNameMaxLength);
        Level = EnglishLevel.Beginner;
        SupportLanguage = "en";
        DailyTargetMinutes = 10;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public EnglishLevel Level { get; private set; }
    public IReadOnlyList<LearningGoal> Goals => _goals;

    /// <summary>BCP 47 language tag used for short explanations (e.g. "en", "hi", "es").</summary>
    public string SupportLanguage { get; private set; } = "en";
    public int DailyTargetMinutes { get; private set; }
    public DateTimeOffset? OnboardingCompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool HasCompletedOnboarding => OnboardingCompletedAt is not null;

    public static LearnerProfile Create(Guid userId, string displayName, DateTimeOffset now) =>
        new(userId, displayName, now);

    public void CompleteOnboarding(
        EnglishLevel level,
        IReadOnlyCollection<LearningGoal> goals,
        string supportLanguage,
        int dailyTargetMinutes,
        DateTimeOffset now)
    {
        UpdatePreferences(level, goals, supportLanguage, dailyTargetMinutes, now);
        OnboardingCompletedAt ??= now;
    }

    public void UpdatePreferences(
        EnglishLevel level,
        IReadOnlyCollection<LearningGoal> goals,
        string supportLanguage,
        int dailyTargetMinutes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(goals);

        if (!Enum.IsDefined(level))
        {
            throw new DomainException("profile.level_invalid", "Unknown English level.");
        }

        var distinctGoals = goals.Distinct().ToList();
        if (distinctGoals.Count is 0 or > MaxGoals)
        {
            throw new DomainException("profile.goals_invalid", $"Choose between 1 and {MaxGoals} goals.");
        }

        if (distinctGoals.Any(g => !Enum.IsDefined(g)))
        {
            throw new DomainException("profile.goals_invalid", "Unknown learning goal.");
        }

        if (dailyTargetMinutes is < MinDailyTargetMinutes or > MaxDailyTargetMinutes)
        {
            throw new DomainException("profile.daily_target_invalid",
                $"Daily target must be between {MinDailyTargetMinutes} and {MaxDailyTargetMinutes} minutes.");
        }

        Level = level;
        _goals = distinctGoals;
        SupportLanguage = Guard.NotBlank(supportLanguage, "profile.support_language_required",
            "Support language is required.", SupportLanguageMaxLength);
        DailyTargetMinutes = dailyTargetMinutes;
        UpdatedAt = now;
    }

    public void Rename(string displayName, DateTimeOffset now)
    {
        DisplayName = Guard.NotBlank(displayName, "profile.display_name_required",
            "Display name is required.", DisplayNameMaxLength);
        UpdatedAt = now;
    }
}
