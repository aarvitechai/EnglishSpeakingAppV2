using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakMate.Domain.Consent;
using SpeakMate.Domain.Usage;

namespace SpeakMate.Infrastructure.Persistence.Configurations;

internal sealed class UsageEventConfiguration : IEntityTypeConfiguration<UsageEvent>
{
    public void Configure(EntityTypeBuilder<UsageEvent> builder)
    {
        builder.ToTable("UsageEvents");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Model).HasMaxLength(UsageEvent.ModelMaxLength).IsRequired();
        builder.Property(u => u.EstimatedCostUsd).HasPrecision(18, 6);

        // Quota checks sum a user's usage over a recent time window.
        builder.HasIndex(u => new { u.UserId, u.CreatedAt })
            .IncludeProperties(u => new { u.InputTokens, u.OutputTokens });
        builder.HasIndex(u => u.SessionId);

        builder.Ignore(u => u.TotalTokens);
    }
}

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.PolicyVersion).HasMaxLength(ConsentRecord.PolicyVersionMaxLength).IsRequired();
        builder.HasIndex(c => new { c.UserId, c.Type });

        builder.Ignore(c => c.IsActive);
    }
}
