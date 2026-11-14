namespace SpeakMate.Domain.Practice;

/// <summary>How a learner turn was produced. Voice turns come from speech recognition and may be inaccurate.</summary>
public enum InputMethod
{
    Text = 1,
    Voice = 2,
}
