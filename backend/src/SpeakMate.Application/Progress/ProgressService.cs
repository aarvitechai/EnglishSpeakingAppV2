using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;
using SpeakMate.Application.Practice;
using SpeakMate.Domain.Feedback;

namespace SpeakMate.Application.Progress;

public sealed record DailyMinutesDto(DateOnly Date, int Minutes);

public sealed record CategoryCountDto(CorrectionCategory Category, int Count);

public sealed record ProgressDto(
    int CompletedSessions,
    int TotalPracticeMinutes,
    int TodayMinutes,
    int DailyTargetMinutes,
    int CurrentStreakDays,
    IReadOnlyList<DailyMinutesDto> Last7Days,
    IReadOnlyList<CategoryCountDto> FocusAreas);

/// <summary>
/// Pure progress maths: easy to unit test and independent of storage.
/// Indicators are transparent counts and minutes, never a proficiency score.
/// </summary>
public static class ProgressCalculator
{
    /// <summary>Single sessions are capped so a tab left open overnight doesn't inflate practice time.</summary>
    public static readonly TimeSpan MaxCountedSessionLength = TimeSpan.FromMinutes(60);

    public static ProgressDto Calculate(
        IReadOnlyList<PracticeActivity> activity,
        IReadOnlyList<CorrectionCategory> categories,
        int completedSessions,
        int dailyTargetMinutes,
        DateTimeOffset now,
        TimeSpan utcOffset)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(categories);

        var today = DateOnly.FromDateTime(now.ToOffset(utcOffset).DateTime);
        var minutesByDay = activity
            .GroupBy(a => DateOnly.FromDateTime(a.StartedAt.ToOffset(utcOffset).DateTime))
            .ToDictionary(g => g.Key, g => g.Sum(a => DurationSeconds(a)) / 60.0);

        int MinutesOn(DateOnly day) => minutesByDay.TryGetValue(day, out var m) ? (int)Math.Round(m) : 0;

        var last7 = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(i - 6))
            .Select(d => new DailyMinutesDto(d, MinutesOn(d)))
            .ToList();

        // A streak counts consecutive days with any practice, ending today (or yesterday, so it
        // doesn't reset just because the learner hasn't practised *yet* today).
        var practisedDays = minutesByDay.Keys.ToHashSet();
        var cursor = practisedDays.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (practisedDays.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        var focus = categories
            .GroupBy(c => c)
            .Select(g => new CategoryCountDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Category)
            .Take(3)
            .ToList();

        return new ProgressDto(
            completedSessions,
            (int)Math.Round(activity.Sum(a => DurationSeconds(a)) / 60.0),
            MinutesOn(today),
            dailyTargetMinutes,
            streak,
            last7,
            focus);
    }

    internal static double DurationSeconds(PracticeActivity a)
    {
        var length = (a.EndedAt ?? a.LastActivityAt) - a.StartedAt;
        if (length < TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Min(length.TotalSeconds, MaxCountedSessionLength.TotalSeconds);
    }
}

public sealed class ProgressService(IPracticeSessionRepository sessions, ILearnerProfileRepository profiles,
    TimeProvider clock)
{
    /// <summary>How far back statistics look.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(90);

    public const int MaxPageSize = 50;

    public async Task<Result<ProgressDto>> GetAsync(Guid userId, int utcOffsetMinutes,
        CancellationToken cancellationToken = default)
    {
        if (utcOffsetMinutes is < -14 * 60 or > 14 * 60)
        {
            return Error.Validation("progress.offset_invalid", "Time-zone offset must be between -840 and 840 minutes.");
        }

        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Error.NotFound("profile.not_found", "Learner profile was not found.");
        }

        var now = clock.GetUtcNow();
        var since = now - Window;
        var activity = await sessions.ListActivityAsync(userId, since, cancellationToken).ConfigureAwait(false);
        var categories = await sessions.ListCorrectionCategoriesAsync(userId, since, cancellationToken).ConfigureAwait(false);
        var completed = await sessions.CountCompletedAsync(userId, cancellationToken).ConfigureAwait(false);

        return ProgressCalculator.Calculate(activity, categories, completed, profile.DailyTargetMinutes, now,
            TimeSpan.FromMinutes(utcOffsetMinutes));
    }

    public async Task<Result<PagedResult<SessionSummaryDto>>> ListHistoryAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return Error.Validation("history.paging_invalid", $"Page must be at least 1 and page size between 1 and {MaxPageSize}.");
        }

        return await sessions.ListForUserAsync(userId, page, pageSize, cancellationToken).ConfigureAwait(false);
    }
}
