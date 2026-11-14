using SpeakMate.Application.Abstractions;
using SpeakMate.Application.Common;
using SpeakMate.Domain.Learners;

namespace SpeakMate.Application.Profiles;

public sealed class ProfileService(ILearnerProfileRepository profiles, IUnitOfWork unitOfWork, TimeProvider clock)
{
    internal static readonly Error ProfileNotFound =
        Error.NotFound("profile.not_found", "Learner profile was not found.");

    public async Task<Result<ProfileDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return profile is null ? ProfileNotFound : ToDto(profile);
    }

    /// <summary>First-time setup. Safe to repeat: later calls just update preferences.</summary>
    public Task<Result<ProfileDto>> CompleteOnboardingAsync(Guid userId, UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(userId, (p, now) =>
            p.CompleteOnboarding(request.Level, request.Goals, request.SupportLanguage, request.DailyTargetMinutes, now),
            cancellationToken);

    public Task<Result<ProfileDto>> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(userId, (p, now) =>
            p.UpdatePreferences(request.Level, request.Goals, request.SupportLanguage, request.DailyTargetMinutes, now),
            cancellationToken);

    public Task<Result<ProfileDto>> RenameAsync(Guid userId, RenameRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(userId, (p, now) => p.Rename(request.DisplayName, now), cancellationToken);

    private async Task<Result<ProfileDto>> MutateAsync(Guid userId, Action<LearnerProfile, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return ProfileNotFound;
        }

        change(profile, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(profile);
    }

    public static ProfileDto ToDto(LearnerProfile p) =>
        new(p.DisplayName, p.Level, p.Goals, p.SupportLanguage, p.DailyTargetMinutes, p.HasCompletedOnboarding);
}
