using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeakMate.Domain.Practice;
using SpeakMate.Infrastructure.Persistence;

namespace SpeakMate.Infrastructure.Retention;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    public bool Enabled { get; set; } = true;

    /// <summary>Active sessions with no activity for this long are closed automatically.</summary>
    public TimeSpan IdleSessionTimeout { get; set; } = TimeSpan.FromHours(2);

    /// <summary>Transcripts without consent are kept at most this long after a session ends (time to retry feedback).</summary>
    public TimeSpan UnconsentedTranscriptGrace { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Expired or revoked refresh tokens are deleted after this long.</summary>
    public TimeSpan RefreshTokenCleanupAfter { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    public int BatchSize { get; set; } = 200;
}

/// <summary>
/// Enforces data-retention rules. All logic runs through domain methods, so the same rules apply
/// here as in requests. Safe to run on several instances: each update uses optimistic concurrency.
/// </summary>
public sealed partial class RetentionService(SpeakMateDbContext db, TimeProvider clock, IOptions<RetentionOptions> options,
    ILogger<RetentionService> logger)
{
    private readonly RetentionOptions _options = options.Value;

    public async Task<RetentionReport> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        // 1. Close idle sessions.
        var idleBefore = now - _options.IdleSessionTimeout;
        var idle = await db.PracticeSessions
            .Where(s => s.Status == SessionStatus.Active && s.LastActivityAt < idleBefore)
            .Include(s => s.Turns)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var cleared = 0;
        foreach (var session in idle)
        {
            session.Abandon(now);
            cleared += session.ClearTranscriptIfNotConsented() ? 1 : 0;
        }

        // 2. Clear unconsented transcripts once the feedback grace period has passed.
        var graceBefore = now - _options.UnconsentedTranscriptGrace;
        var expiredTranscripts = await db.PracticeSessions
            .Where(s => s.Status != SessionStatus.Active && !s.SaveTranscript && !s.TranscriptCleared
                        && s.EndedAt < graceBefore)
            .Include(s => s.Turns)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        cleared += expiredTranscripts.Count(s => s.ClearTranscriptIfNotConsented());

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 3. Delete dead refresh tokens.
        var tokensBefore = now - _options.RefreshTokenCleanupAfter;
        var tokens = await db.RefreshTokens
            .Where(t => t.ExpiresAt < tokensBefore || t.RevokedAt < tokensBefore || t.UsedAt < tokensBefore)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        var report = new RetentionReport(idle.Count, cleared, tokens);
        if (report.HasChanges)
        {
            LogReport(report.SessionsClosed, report.TranscriptsCleared, report.RefreshTokensDeleted);
        }

        return report;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Retention: closed {SessionsClosed} idle sessions, cleared {TranscriptsCleared} transcripts, deleted {TokensDeleted} refresh tokens")]
    private partial void LogReport(int sessionsClosed, int transcriptsCleared, int tokensDeleted);
}

public sealed record RetentionReport(int SessionsClosed, int TranscriptsCleared, int RefreshTokensDeleted)
{
    public bool HasChanges => SessionsClosed + TranscriptsCleared + RefreshTokensDeleted > 0;
}

/// <summary>Runs <see cref="RetentionService"/> on a fixed interval. Failures are logged and retried next tick.</summary>
public sealed partial class RetentionWorker(IServiceScopeFactory scopeFactory, IOptions<RetentionOptions> options,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RetentionService>().RunOnceAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailure(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Retention run failed; will retry on the next interval")]
    private partial void LogFailure(Exception exception);
}
