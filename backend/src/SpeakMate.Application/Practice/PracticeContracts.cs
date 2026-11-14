using FluentValidation;
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
    IReadOnlyList<TurnDto> Turns);

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
