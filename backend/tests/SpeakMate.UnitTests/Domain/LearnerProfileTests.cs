using SpeakMate.Domain.Common;
using SpeakMate.Domain.Learners;

namespace SpeakMate.UnitTests.Domain;

public sealed class LearnerProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static LearnerProfile NewProfile() => LearnerProfile.Create(Guid.NewGuid(), "  Asha  ", Now);

    [Fact]
    public void Create_SetsSafeDefaults()
    {
        var profile = NewProfile();

        Assert.Equal("Asha", profile.DisplayName);
        Assert.Equal(EnglishLevel.Beginner, profile.Level);
        Assert.Equal("en", profile.SupportLanguage);
        Assert.False(profile.HasCompletedOnboarding);
    }

    [Fact]
    public void CompleteOnboarding_StoresPreferencesAndTimestamp()
    {
        var profile = NewProfile();

        profile.CompleteOnboarding(EnglishLevel.Intermediate, [LearningGoal.Work, LearningGoal.Interviews], "hi", 15,
            Now.AddMinutes(1));

        Assert.True(profile.HasCompletedOnboarding);
        Assert.Equal(Now.AddMinutes(1), profile.OnboardingCompletedAt);
        Assert.Equal(EnglishLevel.Intermediate, profile.Level);
        Assert.Equal([LearningGoal.Work, LearningGoal.Interviews], profile.Goals);
        Assert.Equal("hi", profile.SupportLanguage);
        Assert.Equal(15, profile.DailyTargetMinutes);
    }

    [Fact]
    public void CompleteOnboarding_Twice_KeepsOriginalCompletionTime()
    {
        var profile = NewProfile();
        profile.CompleteOnboarding(EnglishLevel.Beginner, [LearningGoal.Travel], "en", 10, Now);

        profile.CompleteOnboarding(EnglishLevel.Advanced, [LearningGoal.Study], "en", 20, Now.AddDays(1));

        Assert.Equal(Now, profile.OnboardingCompletedAt);
        Assert.Equal(EnglishLevel.Advanced, profile.Level);
    }

    [Fact]
    public void UpdatePreferences_RemovesDuplicateGoals()
    {
        var profile = NewProfile();

        profile.UpdatePreferences(EnglishLevel.Beginner, [LearningGoal.Travel, LearningGoal.Travel], "en", 10, Now);

        Assert.Equal([LearningGoal.Travel], profile.Goals);
    }

    [Fact]
    public void UpdatePreferences_WithNoGoals_Throws()
    {
        var ex = Assert.Throws<DomainException>(() =>
            NewProfile().UpdatePreferences(EnglishLevel.Beginner, [], "en", 10, Now));

        Assert.Equal("profile.goals_invalid", ex.Code);
    }

    [Fact]
    public void UpdatePreferences_WithTooManyGoals_Throws() =>
        Assert.Throws<DomainException>(() => NewProfile().UpdatePreferences(EnglishLevel.Beginner,
            [LearningGoal.Travel, LearningGoal.Work, LearningGoal.Study, LearningGoal.Confidence], "en", 10, Now));

    [Theory]
    [InlineData(LearnerProfile.MinDailyTargetMinutes - 1)]
    [InlineData(LearnerProfile.MaxDailyTargetMinutes + 1)]
    public void UpdatePreferences_WithDailyTargetOutOfRange_Throws(int minutes)
    {
        var ex = Assert.Throws<DomainException>(() =>
            NewProfile().UpdatePreferences(EnglishLevel.Beginner, [LearningGoal.Travel], "en", minutes, Now));

        Assert.Equal("profile.daily_target_invalid", ex.Code);
    }

    [Fact]
    public void UpdatePreferences_WithUnknownLevel_Throws() =>
        Assert.Throws<DomainException>(() =>
            NewProfile().UpdatePreferences((EnglishLevel)42, [LearningGoal.Travel], "en", 10, Now));

    [Fact]
    public void Rename_WithBlankName_Throws() =>
        Assert.Throws<DomainException>(() => NewProfile().Rename(" ", Now));
}
