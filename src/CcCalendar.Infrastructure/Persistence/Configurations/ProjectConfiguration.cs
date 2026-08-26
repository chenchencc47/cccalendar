using CcCalendar.Core.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Name).HasMaxLength(200).IsRequired();
        builder.Property(project => project.Color).HasMaxLength(7).IsFixedLength().IsRequired();
        builder.Property(project => project.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Ignore(project => project.IsDeleted);
        builder.HasQueryFilter(project => project.DeletedAtUtc == null);

        builder.HasMany(project => project.Milestones)
            .WithOne()
            .HasForeignKey("ProjectId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(project => project.Milestones).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(project => project.Participants)
            .WithOne()
            .HasForeignKey("ProjectId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(project => project.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
