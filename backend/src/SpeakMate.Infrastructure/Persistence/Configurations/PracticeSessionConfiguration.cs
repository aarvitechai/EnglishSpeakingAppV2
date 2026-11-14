using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakMate.Domain.Feedback;
using SpeakMate.Domain.Practice;

namespace SpeakMate.Infrastructure.Persistence.Configurations;

internal sealed class PracticeSessionConfiguration : IEntityTypeConfiguration<PracticeSession>
{
    public void Configure(EntityTypeBuilder<PracticeSession> builder)
    {
        builder.ToTable("PracticeSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Topic).HasMaxLength(PracticeSession.TopicMaxLength).IsRequired();
        builder.Property(s => s.RowVersion).IsRowVersion();

        // History pages list a user's sessions newest-first.
        builder.HasIndex(s => new { s.UserId, s.StartedAt }).IsDescending(false, true);
        // Fast lookup of a user's active sessions (filtered index keeps it small).
        builder.HasIndex(s => new { s.UserId, s.Status })
            .HasFilter("[Status] = 'Active'")
            .HasDatabaseName("IX_PracticeSessions_UserId_Active");

        builder.HasMany(s => s.Turns)
            .WithOne()
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Turns).HasField("_turns").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(s => s.Feedback)
            .WithOne()
            .HasForeignKey<SessionFeedback>(f => f.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(s => s.IsActive);
        builder.Ignore(s => s.HasReachedTurnLimit);
        builder.Ignore(s => s.Duration);
    }
}

internal sealed class ConversationTurnConfiguration : IEntityTypeConfiguration<ConversationTurn>
{
    public void Configure(EntityTypeBuilder<ConversationTurn> builder)
    {
        builder.ToTable("ConversationTurns");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Text).HasMaxLength(ConversationTurn.MaxTextLength).IsRequired();
        builder.HasIndex(t => new { t.SessionId, t.Sequence }).IsUnique();
    }
}

internal sealed class SessionFeedbackConfiguration : IEntityTypeConfiguration<SessionFeedback>
{
    public void Configure(EntityTypeBuilder<SessionFeedback> builder)
    {
        builder.ToTable("SessionFeedback");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.HasIndex(f => f.SessionId).IsUnique();

        builder.Property(f => f.Summary).HasMaxLength(SessionFeedback.SummaryMaxLength).IsRequired();
        builder.Property(f => f.RepeatTask).HasMaxLength(SessionFeedback.RepeatTaskMaxLength).IsRequired();

        // Corrections are always read with their feedback, so a JSON column avoids an extra join.
        builder.OwnsMany(f => f.Corrections, c =>
        {
            c.ToJson("Corrections");
            c.Property(x => x.Category).HasConversion<string>();
        });
        builder.Navigation(f => f.Corrections).HasField("_corrections").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(f => f.UsefulPhrases);
        builder.PrimitiveCollection<List<string>>("_usefulPhrases")
            .HasColumnName("UsefulPhrases")
            .HasField("_usefulPhrases")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();
    }
}
