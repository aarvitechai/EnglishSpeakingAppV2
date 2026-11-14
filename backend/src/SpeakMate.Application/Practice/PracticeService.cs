using Microsoft.Extensions.Logging;
using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;
using SpeakMate.Application.Usage;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;
using SpeakMate.Domain.Usage;

namespace SpeakMate.Application.Practice;

/// <summary>
/// Conversation use cases: start a session (coach opens), send a learner message (coach replies), read a session.
/// An AI failure never leaves a half-saved turn: the learner can simply resend the same message.
/// </summary>
public sealed partial class PracticeService(
    IPracticeSessionRepository sessions,
    ILearnerProfileRepository profiles,
    IConsentRepository consents,
    IUsageEventRepository usageEvents,
    IUsageRecorder usageRecorder,
    IUnitOfWork unitOfWork,
    IAiCoach coach,
    UsageGuard usageGuard,
    UsageLimits limits,
    TimeProvider clock,
    ILogger<PracticeService> logger)
{
    internal static readonly Error SessionNotFound =
        Error.NotFound("session.not_found", "Practice session was not found.");

    internal static readonly Error OnboardingRequired =
        Error.Validation("session.onboarding_required", "Finish setting up your profile before practising.");

    internal static readonly Error CoachUnavailable =
        Error.Unavailable("ai.unavailable", "Your coach is having trouble responding. Please try again in a moment.");

    public async Task<Result<SessionDto>> StartAsync(Guid userId, StartSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile is null || !profile.HasCompletedOnboarding)
        {
            return OnboardingRequired;
        }

        var active = await sessions.CountActiveForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        if (active >= limits.MaxActiveSessionsPerUser)
        {
            return Error.Conflict("session.too_many_active",
                $"You already have {active} unfinished sessions. Finish or end one first.");
        }

        if (await usageGuard.CheckDailyQuotaAsync(userId, cancellationToken).ConfigureAwait(false) is { } quotaError)
        {
            return quotaError;
        }

        var topic = string.IsNullOrWhiteSpace(request.Topic)
            ? TopicCatalog.Pick(request.Mode, Random.Shared)
            : request.Topic.Trim();
        var saveTranscript = await HasTranscriptConsentAsync(userId, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var session = PracticeSession.Start(userId, request.Mode, profile.Level, topic, saveTranscript, now);

        var reply = await AskCoachAsync(userId, session, profile, cancellationToken).ConfigureAwait(false);
        if (reply is null)
        {
            return CoachUnavailable;
        }

        session.AddCoachTurn(reply.Text, clock.GetUtcNow());
        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(session);
    }

    public async Task<Result<SessionDto>> SendMessageAsync(Guid userId, Guid sessionId, SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await sessions.GetForUserAsync(sessionId, userId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionNotFound;
        }

        if (!session.IsActive)
        {
            return Error.Conflict("session.not_active", "This practice session has already ended.");
        }

        if (session.HasReachedTurnLimit)
        {
            return Error.Conflict("session.turn_limit",
                "This session reached its message limit. Finish it to see your feedback.");
        }

        if (await usageGuard.CheckDailyQuotaAsync(userId, cancellationToken).ConfigureAwait(false) is { } quotaError)
        {
            return quotaError;
        }

        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return OnboardingRequired;
        }

        // Domain rules (turn order, length, confidence range) are enforced here; violations become 400s.
        session.AddLearnerTurn(request.Text, request.InputMethod, request.SpeechConfidence, clock.GetUtcNow());

        var reply = await AskCoachAsync(userId, session, profile, cancellationToken).ConfigureAwait(false);
        if (reply is null)
        {
            return CoachUnavailable; // Nothing saved: the learner turn was only added in memory.
        }

        session.AddCoachTurn(reply.Text, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(session);
    }

    public async Task<Result<SessionDto>> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetForUserAsync(sessionId, userId, cancellationToken).ConfigureAwait(false);
        return session is null ? SessionNotFound : ToDto(session);
    }

    /// <summary>Calls the coach and records usage either way. Returns null if the provider failed.</summary>
    private async Task<CoachReply?> AskCoachAsync(Guid userId, PracticeSession session, LearnerProfile profile,
        CancellationToken cancellationToken)
    {
        var context = new CoachContext(
            session.Mode,
            session.Level,
            session.Topic,
            profile.Goals,
            profile.SupportLanguage,
            session.Turns.Select(t => new CoachTurn(t.Speaker, t.Text, t.InputMethod, t.SpeechConfidence)).ToList());

        try
        {
            var reply = await coach.GenerateReplyAsync(context, cancellationToken).ConfigureAwait(false);
            RecordUsage(userId, session.Id, reply.Usage, succeeded: true);
            return reply;
        }
        catch (AiProviderException ex)
        {
            LogCoachFailure(ex, session.Id);
            if (ex.Usage is not null)
            {
                // Failed calls can still cost tokens. Save the usage in an isolated scope so the
                // unsaved learner turn in this unit of work is NOT persisted with it.
                await usageRecorder.SaveIsolatedAsync(CreateUsageEvent(userId, session.Id, ex.Usage, succeeded: false),
                    cancellationToken).ConfigureAwait(false);
            }

            return null;
        }
    }

    private void RecordUsage(Guid userId, Guid sessionId, AiUsage aiUsage, bool succeeded) =>
        usageEvents.Add(CreateUsageEvent(userId, sessionId, aiUsage, succeeded));

    private UsageEvent CreateUsageEvent(Guid userId, Guid sessionId, AiUsage aiUsage, bool succeeded) =>
        UsageEvent.Record(userId, sessionId, AiOperation.ConversationReply, aiUsage.Model,
            aiUsage.InputTokens, aiUsage.OutputTokens, usageGuard.EstimateCostUsd(aiUsage), succeeded,
            aiUsage.DurationMs, clock.GetUtcNow());

    private async Task<bool> HasTranscriptConsentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var records = await consents.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return records.Where(r => r.Type == ConsentType.TranscriptStorage).MaxBy(r => r.GrantedAt)?.IsActive ?? false;
    }

    internal static SessionDto ToDto(PracticeSession s) =>
        new(s.Id, s.Mode, s.Level, s.Topic, s.Status, s.SaveTranscript, s.TranscriptCleared, s.StartedAt, s.EndedAt,
            s.LearnerTurnCount, PracticeSession.MaxLearnerTurns,
            s.Turns.OrderBy(t => t.Sequence)
                .Select(t => new TurnDto(t.Sequence, t.Speaker, t.Text, t.InputMethod, t.CreatedAt))
                .ToList());

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI coach failed for session {SessionId}")]
    private partial void LogCoachFailure(Exception exception, Guid sessionId);
}
