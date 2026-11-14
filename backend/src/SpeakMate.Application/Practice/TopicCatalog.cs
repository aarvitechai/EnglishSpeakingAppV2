using SpeakMate.Domain.Practice;

namespace SpeakMate.Application.Practice;

/// <summary>Curated starter topics per mode, used when the learner doesn't type their own.</summary>
public static class TopicCatalog
{
    private static readonly Dictionary<PracticeMode, string[]> Topics = new()
    {
        [PracticeMode.DailyConversation] =
        [
            "Your weekend", "Favourite food", "Your hometown", "Hobbies and free time", "A good film or series",
            "Your daily routine", "Weather and seasons", "Friends and family",
        ],
        [PracticeMode.RolePlay] =
        [
            "Ordering at a restaurant", "Checking in at a hotel", "Buying clothes in a shop",
            "Asking for directions", "Booking a doctor's appointment", "Returning a faulty product",
        ],
        [PracticeMode.InterviewPractice] =
        [
            "General job interview", "Software developer interview", "Customer service role interview",
            "University admission interview", "Internship interview",
        ],
        [PracticeMode.TopicChallenge] =
        [
            "Technology in everyday life", "The best way to learn a language", "City life or village life",
            "A person who inspires you", "Working from home",
        ],
        [PracticeMode.GuidedBeginner] =
        [
            "Introducing yourself", "Numbers and time", "Colours and clothes", "Food you like", "Your family",
        ],
    };

    public static IReadOnlyList<string> For(PracticeMode mode) =>
        Topics.TryGetValue(mode, out var topics) ? topics : Topics[PracticeMode.DailyConversation];

    public static IReadOnlyDictionary<PracticeMode, string[]> All => Topics;

    public static string Pick(PracticeMode mode, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var topics = For(mode);
        return topics[random.Next(topics.Count)];
    }
}
