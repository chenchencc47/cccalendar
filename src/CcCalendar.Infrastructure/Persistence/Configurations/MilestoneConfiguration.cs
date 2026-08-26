using CcCalendar.Core.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones");
        builder.HasKey(milestone => milestone.Id);
        builder.Property(milestone => milestone.Title).HasMaxLength(200).IsRequired();
        builder.Ignore(milestone => milestone.IsCompleted);
    }
}
