using FluentValidation;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Application.Practice;

public sealed record StartSessionRequest(PracticeMode Mode, string? Topic);

public sealed record SendMessageRequest(string Text, InputMethod InputMethod, double? SpeechConfidence);

public sealed record TurnDto(int Sequence, Speaker Speaker, string Text, InputMethod InputMethod, DateTimeOffset CreatedAt);

public sealed record SessionDto(
    Guid Id,
    PracticeMode Mode,
    EnglishLevel Level,
    string Topic,
    SessionStatus Status,
    bool SaveTranscript,
    bool TranscriptCleared,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int LearnerTurnCount,
    int MaxLearnerTurns,
    IReadOnlyList<TurnDto> Turns,
    FeedbackDto? Feedback);

public sealed record CorrectionDto(CorrectionCategory Category, string Original, string Improved, string Explanation);

public sealed record FeedbackDto(
    string Summary,
    IReadOnlyList<CorrectionDto> Corrections,
    IReadOnlyList<string> UsefulPhrases,
    string RepeatTask,
    bool? WasHelpful,
    DateTimeOffset CreatedAt);

public sealed record SessionSummaryDto(
    Guid Id,
    PracticeMode Mode,
    string Topic,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int DurationSeconds,
    int LearnerTurnCount,
    bool HasFeedback);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record RateFeedbackRequest(bool WasHelpful);

public sealed record TopicSuggestionsDto(PracticeMode Mode, IReadOnlyList<string> Topics);

internal sealed class StartSessionRequestValidator : AbstractValidator<StartSessionRequest>
{
    public StartSessionRequestValidator()
    {
        RuleFor(r => r.Mode).IsInEnum();
        RuleFor(r => r.Topic).MaximumLength(PracticeSession.TopicMaxLength);
    }
}

internal sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(r => r.Text).NotEmpty().MaximumLength(ConversationTurn.MaxTextLength);
        RuleFor(r => r.InputMethod).IsInEnum();
        RuleFor(r => r.SpeechConfidence).InclusiveBetween(0, 1).When(r => r.SpeechConfidence is not null);
    }
}
