using SpeakMate.Domain.Common;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Feedback;

namespace SpeakMate.UnitTests.Domain;

public sealed class FeedbackAndConsentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static Correction AnyCorrection(int i = 0) =>
        new(CorrectionCategory.Grammar, $"I go yesterday {i}", $"I went yesterday {i}", "Use past tense for finished actions.");

    [Fact]
    public void Feedback_WithUpToThreeCorrections_IsCreated()
    {
        var feedback = SessionFeedback.Create(Guid.NewGuid(), "Nice work.",
            [AnyCorrection(1), AnyCorrection(2), AnyCorrection(3)], ["How about…?"], "Retell the story.", Now);

        Assert.Equal(3, feedback.Corrections.Count);
        Assert.Null(feedback.WasHelpful);
    }

    [Fact]
    public void Feedback_WithMoreThanThreeCorrections_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => SessionFeedback.Create(Guid.NewGuid(), "Summary",
            Enumerable.Range(0, 4).Select(AnyCorrection), [], "Task", Now));

        Assert.Equal("feedback.too_many_corrections", ex.Code);
    }

    [Fact]
    public void Feedback_DeduplicatesPhrasesIgnoringCase()
    {
        var feedback = SessionFeedback.Create(Guid.NewGuid(), "Summary", [],
            ["Could you repeat that?", "could you repeat that?"], "Task", Now);

        Assert.Single(feedback.UsefulPhrases);
    }

    [Fact]
    public void Feedback_WithTooManyPhrases_Throws() =>
        Assert.Throws<DomainException>(() => SessionFeedback.Create(Guid.NewGuid(), "Summary", [],
            Enumerable.Range(0, SessionFeedback.MaxUsefulPhrases + 1).Select(i => $"Phrase {i}"), "Task", Now));

    [Fact]
    public void Feedback_RateHelpfulness_RecordsAnswer()
    {
        var feedback = SessionFeedback.Create(Guid.NewGuid(), "Summary", [], [], "Task", Now);

        feedback.RateHelpfulness(true);

        Assert.True(feedback.WasHelpful);
    }

    [Fact]
    public void Correction_WithBlankImprovedText_Throws() =>
        Assert.Throws<DomainException>(() =>
            new Correction(CorrectionCategory.Vocabulary, "big", " ", "Explanation"));

    [Fact]
    public void Consent_Withdraw_OptionalConsent_IsRecordedOnce()
    {
        var consent = ConsentRecord.Grant(Guid.NewGuid(), ConsentType.TranscriptStorage, "2026-11", Now);

        consent.Withdraw(Now.AddDays(1));
        consent.Withdraw(Now.AddDays(2));

        Assert.False(consent.IsActive);
        Assert.Equal(Now.AddDays(1), consent.WithdrawnAt);
    }

    [Fact]
    public void Consent_Withdraw_TermsConsent_Throws()
    {
        var consent = ConsentRecord.Grant(Guid.NewGuid(), ConsentType.TermsAndPrivacy, "2026-11", Now);

        var ex = Assert.Throws<DomainException>(() => consent.Withdraw(Now));

        Assert.Equal("consent.required", ex.Code);
    }
}
