using SpeakMate.Domain.Common;

namespace SpeakMate.Domain.Practice;

/// <summary>One message in a practice conversation. Created only through <see cref="PracticeSession"/>.</summary>
public sealed class ConversationTurn : Entity
{
    public const int MaxTextLength = 2000;

    private ConversationTurn() { } // EF Core

    internal ConversationTurn(Guid sessionId, int sequence, Speaker speaker, string text,
        InputMethod inputMethod, double? speechConfidence, DateTimeOffset now)
    {
        SessionId = sessionId;
        Sequence = sequence;
        Speaker = speaker;
        Text = Guard.NotBlank(text, "turn.text_required", "Message text is required.", MaxTextLength);
        InputMethod = inputMethod;
        SpeechConfidence = speechConfidence;
        CreatedAt = now;
    }

    public Guid SessionId { get; private set; }

    /// <summary>1-based position within the session; a stable order that doesn't depend on clocks.</summary>
    public int Sequence { get; private set; }
    public Speaker Speaker { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public InputMethod InputMethod { get; private set; }

    /// <summary>Speech-recognition confidence from 0 to 1, when the recogniser provides it.</summary>
    public double? SpeechConfidence { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
