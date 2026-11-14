using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Progress;
using SpeakMate.Domain.Feedback;

namespace SpeakMate.UnitTests.Application;

public sealed class ProgressCalculatorTests
{
    // Saturday 14 Nov 2026, 20:00 UTC.
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 20, 0, 0, TimeSpan.Zero);

    private static PracticeActivity Session(DateTimeOffset start, int minutes) =>
        new(start, start.AddMinutes(minutes), start.AddMinutes(minutes));

    private static ProgressDto Calc(IReadOnlyList<PracticeActivity> activity, TimeSpan? offset = null,
        IReadOnlyList<CorrectionCategory>? categories = null) =>
        ProgressCalculator.Calculate(activity, categories ?? [], completedSessions: activity.Count, dailyTargetMinutes: 10,
            Now, offset ?? TimeSpan.Zero);

    [Fact]
    public void NoActivity_ReturnsZerosAndSevenEmptyDays()
    {
        var progress = Calc([]);

        Assert.Equal(0, progress.TotalPracticeMinutes);
        Assert.Equal(0, progress.CurrentStreakDays);
        Assert.Equal(7, progress.Last7Days.Count);
        Assert.All(progress.Last7Days, d => Assert.Equal(0, d.Minutes));
        Assert.Equal(new DateOnly(2026, 11, 14), progress.Last7Days[^1].Date);
    }

    [Fact]
    public void SumsMinutesPerDayAndToday()
    {
        var progress = Calc(
        [
            Session(Now.AddHours(-2), 12),
            Session(Now.AddHours(-1), 8),
            Session(Now.AddDays(-1), 5),
        ]);

        Assert.Equal(25, progress.TotalPracticeMinutes);
        Assert.Equal(20, progress.TodayMinutes);
        Assert.Equal(5, progress.Last7Days[^2].Minutes);
    }

    [Fact]
    public void Streak_CountsConsecutiveDaysEndingToday()
    {
        var progress = Calc([Session(Now, 5), Session(Now.AddDays(-1), 5), Session(Now.AddDays(-2), 5), Session(Now.AddDays(-4), 5)]);

        Assert.Equal(3, progress.CurrentStreakDays);
    }

    [Fact]
    public void Streak_SurvivesUntilEndOfDayIfYesterdayWasPractised()
    {
        var progress = Calc([Session(Now.AddDays(-1), 5), Session(Now.AddDays(-2), 5)]);

        Assert.Equal(2, progress.CurrentStreakDays);
    }

    [Fact]
    public void Streak_BreaksAfterAMissedDay()
    {
        var progress = Calc([Session(Now.AddDays(-2), 5)]);

        Assert.Equal(0, progress.CurrentStreakDays);
    }

    [Fact]
    public void UsesLearnersTimeZoneForDayBoundaries()
    {
        // 20:00 UTC on the 14th is 01:30 on the 15th in India (UTC+5:30).
        var progress = Calc([Session(Now.AddMinutes(-10), 10)], TimeSpan.FromMinutes(330));

        Assert.Equal(new DateOnly(2026, 11, 15), progress.Last7Days[^1].Date);
        Assert.Equal(10, progress.TodayMinutes);
    }

    [Fact]
    public void CapsVeryLongSessions()
    {
        var progress = Calc([Session(Now.AddHours(-10), 600)]);

        Assert.Equal(60, progress.TotalPracticeMinutes);
    }

    [Fact]
    public void FocusAreas_AreTopThreeCategoriesByFrequency()
    {
        var progress = Calc([], categories:
        [
            CorrectionCategory.Grammar, CorrectionCategory.Grammar, CorrectionCategory.Grammar,
            CorrectionCategory.Vocabulary, CorrectionCategory.Vocabulary,
            CorrectionCategory.Fluency,
            CorrectionCategory.NaturalPhrasing,
        ]);

        Assert.Equal([CorrectionCategory.Grammar, CorrectionCategory.Vocabulary, CorrectionCategory.NaturalPhrasing],
            progress.FocusAreas.Select(f => f.Category));
        Assert.Equal(3, progress.FocusAreas[0].Count);
    }
}
