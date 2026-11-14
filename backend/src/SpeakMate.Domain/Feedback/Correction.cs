using SpeakMate.Domain.Common;

namespace SpeakMate.Domain.Feedback;

public enum CorrectionCategory
{
    Grammar = 1,
    Vocabulary = 2,
    NaturalPhrasing = 3,
    Fluency = 4,
    Pronunciation = 5,
}

/// <summary>A single improvement: what the learner said, a better version, and why.</summary>
public sealed record Correction
{
    public const int TextMaxLength = 500;

    public Correction(CorrectionCategory category, string original, string improved, string explanation)
    {
        if (!Enum.IsDefined(category))
        {
            throw new DomainException("correction.category_invalid", "Unknown correction category.");
        }

        Category = category;
        Original = Guard.NotBlank(original, "correction.original_required", "Original text is required.", TextMaxLength);
        Improved = Guard.NotBlank(improved, "correction.improved_required", "Improved text is required.", TextMaxLength);
        Explanation = Guard.NotBlank(explanation, "correction.explanation_required",
            "Explanation is required.", TextMaxLength);
    }

    public CorrectionCategory Category { get; init; }
    public string Original { get; init; }
    public string Improved { get; init; }
    public string Explanation { get; init; }
}
