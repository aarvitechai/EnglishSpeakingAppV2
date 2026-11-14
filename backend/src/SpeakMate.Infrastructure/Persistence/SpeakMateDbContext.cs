using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpeakMate.Application.Abstractions;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Learners;
using SpeakMate.Domain.Practice;
using SpeakMate.Domain.Usage;

namespace SpeakMate.Infrastructure.Persistence;

public class SpeakMateDbContext(DbContextOptions<SpeakMateDbContext> options) : DbContext(options), IUnitOfWork
{
    public const string Schema = "app";

    public DbSet<LearnerProfile> LearnerProfiles => Set<LearnerProfile>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
    public DbSet<ConversationTurn> ConversationTurns => Set<ConversationTurn>();
    public DbSet<SessionFeedback> SessionFeedback => Set<SessionFeedback>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpeakMateDbContext).Assembly);
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
}
