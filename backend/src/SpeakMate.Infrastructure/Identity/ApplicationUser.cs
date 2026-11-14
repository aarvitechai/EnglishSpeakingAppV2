using Microsoft.AspNetCore.Identity;

namespace SpeakMate.Infrastructure.Identity;

/// <summary>Login account managed by ASP.NET Core Identity. Learner data lives in <c>LearnerProfile</c>.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public DateTimeOffset CreatedAt { get; set; }
}
