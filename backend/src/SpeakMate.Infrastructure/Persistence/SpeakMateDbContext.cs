using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;
using SpeakMate.Domain.Usage;
using SpeakMate.Infrastructure.Identity;

namespace SpeakMate.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the app. Learner data lives in the <c>app</c> schema and
/// login data in the <c>auth</c> schema. Every learner table cascades from <c>auth.Users</c>,
/// so deleting an account removes all of that person's data.
/// </summary>
public class SpeakMateDbContext(DbContextOptions<SpeakMateDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IUnitOfWork
{
    public const string Schema = "app";
    public const string AuthSchema = "auth";

    public DbSet<LearnerProfile> LearnerProfiles => Set<LearnerProfile>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
    public DbSet<ConversationTurn> ConversationTurns => Set<ConversationTurn>();
    public DbSet<SessionFeedback> SessionFeedback => Set<SessionFeedback>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<ApplicationUser>().ToTable("Users", AuthSchema);
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", AuthSchema);
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", AuthSchema);
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", AuthSchema);
        builder.Entity<ApplicationUser>().HasIndex(u => u.NormalizedEmail).IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.ApplyConfigurationsFromAssembly(typeof(SpeakMateDbContext).Assembly);

        CascadeFromUser<LearnerProfile>(builder, nameof(LearnerProfile.UserId));
        CascadeFromUser<PracticeSession>(builder, nameof(PracticeSession.UserId));
        CascadeFromUser<UsageEvent>(builder, nameof(UsageEvent.UserId));
        CascadeFromUser<ConsentRecord>(builder, nameof(ConsentRecord.UserId));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Enums are stored as readable strings: safer to reorder and easier to query by hand.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The data was changed by another request. Please retry.", ex);
        }
        catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
        {
            // A concurrent request created the same row first (e.g. two replies racing for turn #N).
            throw new ConcurrencyConflictException("Another request saved conflicting data first. Please retry.", ex);
        }
    }

    // 2601: duplicate key in unique index; 2627: unique/primary key constraint violation.
    private static bool IsUniqueKeyViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static void CascadeFromUser<TEntity>(ModelBuilder modelBuilder, string foreignKey)
        where TEntity : class =>
        modelBuilder.Entity<TEntity>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(foreignKey)
            .OnDelete(DeleteBehavior.Cascade);
}
