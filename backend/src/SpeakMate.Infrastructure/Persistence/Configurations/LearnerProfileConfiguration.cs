using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakMate.Domain.Learners;

namespace SpeakMate.Infrastructure.Persistence.Configurations;

internal sealed class LearnerProfileConfiguration : IEntityTypeConfiguration<LearnerProfile>
{
    public void Configure(EntityTypeBuilder<LearnerProfile> builder)
    {
        builder.ToTable("LearnerProfiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasIndex(p => p.UserId).IsUnique();
        builder.Property(p => p.DisplayName).HasMaxLength(LearnerProfile.DisplayNameMaxLength).IsRequired();
        builder.Property(p => p.SupportLanguage).HasMaxLength(LearnerProfile.SupportLanguageMaxLength).IsRequired();

        // Goals are a small list of enum values, stored as a JSON column through the private backing field.
        builder.Ignore(p => p.Goals);
        builder.PrimitiveCollection<List<LearningGoal>>("_goals")
            .HasColumnName("Goals")
            .HasField("_goals")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        builder.Ignore(p => p.HasCompletedOnboarding);
    }
}
