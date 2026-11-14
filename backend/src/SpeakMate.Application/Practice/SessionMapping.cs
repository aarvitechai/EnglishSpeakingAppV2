using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Application.Practice;

/// <summary>Shared mapping between domain objects, AI context and API DTOs.</summary>
public static class SessionMapping
{
    public static CoachContext ToCoachContext(PracticeSession session, LearnerProfile profile) =>
        new(session.Mode,
            session.Level,
            session.Topic,
            profile.Goals,
            profile.SupportLanguage,
            session.Turns
                .OrderBy(t => t.Sequence)
                .Select(t => new CoachTurn(t.Speaker, t.Text, t.InputMethod, t.SpeechConfidence))
                .ToList());

    public static SessionDto ToDto(PracticeSession s) =>
        new(s.Id, s.Mode, s.Level, s.Topic, s.Status, s.SaveTranscript, s.TranscriptCleared, s.StartedAt, s.EndedAt,
            s.LearnerMessageCount, PracticeSession.MaxLearnerTurns,
            s.Turns.OrderBy(t => t.Sequence)
                .Select(t => new TurnDto(t.Sequence, t.Speaker, t.Text, t.InputMethod, t.CreatedAt))
                .ToList(),
            s.Feedback is null ? null : ToDto(s.Feedback));

    public static FeedbackDto ToDto(SessionFeedback f) =>
        new(f.Summary,
            f.Corrections.Select(c => new CorrectionDto(c.Category, c.Original, c.Improved, c.Explanation)).ToList(),
            f.UsefulPhrases,
            f.RepeatTask,
            f.WasHelpful,
            f.CreatedAt);
}
