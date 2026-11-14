using Microsoft.Extensions.Logging;
using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;
using SpeakMate.Application.Practice;
using SpeakMate.Application.Usage;
using SpeakMate.Domain.Practice;
using SpeakMate.Domain.Usage;

namespace SpeakMate.Application.Feedback;

/// <summary>
/// Ends sessions and produces their feedback. Finishing always succeeds (even if the AI is down);
/// feedback can then be retried. Transcripts without consent are cleared once feedback exists.
/// </summary>
public sealed partial class SessionFeedbackService(
    IPracticeSessionRepository sessions,
    ILearnerProfileRepository profiles,
    IUsageEventRepository usageEvents,
    IUnitOfWork unitOfWork,
    IAiCoach coach,
    UsageGuard usageGuard,
    TimeProvider clock,
    ILogger<SessionFeedbackService> logger)
{
    /// <summary>Completes the session and generates feedback. Idempotent for already-finished sessions.</summary>
    public async Task<Result<SessionDto>> FinishAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetForUserAsync(sessionId, userId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return PracticeService.SessionNotFound;
        }

        switch (session.Status)
        {
            case SessionStatus.Abandoned:
                return Error.Conflict("session.abandoned", "This session was ended without feedback.");
            case SessionStatus.Completed when session.Feedback is not null:
                return SessionMapping.ToDto(session);
            case SessionStatus.Active:
                session.Complete(clock.GetUtcNow()); // throws session.empty (400) if nothing was said
                break;
        }

        await TryAttachFeedbackAsync(userId, session, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SessionMapping.ToDto(session);
    }

    /// <summary>Retries feedback for a completed session whose first attempt failed.</summary>
    public async Task<Result<SessionDto>> RetryFeedbackAsync(Guid userId, Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetForUserAsync(sessionId, userId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return PracticeService.SessionNotFound;
        }

        if (session.Status != SessionStatus.Completed)
        {
            return Error.Conflict("session.not_completed", "Finish the session before requesting feedback.");
        }

        if (session.Feedback is not null)
        {
            return SessionMapping.ToDto(session);
        }

        if (session.TranscriptCleared)
        {
            return Error.Conflict("feedback.transcript_unavailable",
                "Feedback can no longer be generated because this conversation was not kept.");
        }

        var attached = await TryAttachFeedbackAsync(userId, session, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return attached ? SessionMapping.ToDto(session) : PracticeService.CoachUnavailable;
    }

    /// <summary>Ends a session without feedback (learner chose to stop).</summary>
    public async Task<Result<SessionDto>> AbandonAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetForUserAsync(sessionId, userId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return PracticeService.SessionNotFound;
        }

        if (!session.IsActive)
        {
            return Error.Conflict("session.not_active", "This practice session has already ended.");
        }

        session.Abandon(clock.GetUtcNow());
        session.ClearTranscriptIfNotConsented();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SessionMapping.ToDto(session);
    }

    public async Task<Result<SessionDto>> RateAsync(Guid userId, Guid sessionId, bool wasHelpful,
        CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetForUserAsync(sessionId, userId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return PracticeService.SessionNotFound;
        }

        if (session.Feedback is null)
        {
            return Error.Conflict("feedback.missing", "This session has no feedback to rate yet.");
        }

        session.Feedback.RateHelpfulness(wasHelpful);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SessionMapping.ToDto(session);
    }

    private async Task<bool> TryAttachFeedbackAsync(Guid userId, PracticeSession session, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return false;
        }

        try
        {
            var draft = await coach.GenerateFeedbackAsync(SessionMapping.ToCoachContext(session, profile), cancellationToken)
                .ConfigureAwait(false);
            RecordUsage(userId, session.Id, draft.Usage, succeeded: true);

            session.AttachFeedback(FeedbackSanitizer.ToDomain(session.Id, draft, clock.GetUtcNow()));
            session.ClearTranscriptIfNotConsented();
            return true;
        }
        catch (AiProviderException ex)
        {
            // The session stays completed (saved by the caller) and the learner can retry feedback.
            LogFeedbackFailure(ex, session.Id);
            if (ex.Usage is not null)
            {
                RecordUsage(userId, session.Id, ex.Usage, succeeded: false);
            }

            return false;
        }
    }

    private void RecordUsage(Guid userId, Guid sessionId, AiUsage usage, bool succeeded) =>
        usageEvents.Add(UsageEvent.Record(userId, sessionId, AiOperation.SessionFeedback, usage.Model,
            usage.InputTokens, usage.OutputTokens, usageGuard.EstimateCostUsd(usage), succeeded, usage.DurationMs,
            clock.GetUtcNow()));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Feedback generation failed for session {SessionId}")]
    private partial void LogFeedbackFailure(Exception exception, Guid sessionId);
}
