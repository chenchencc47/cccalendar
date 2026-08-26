using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class WorkRecordConfiguration : IEntityTypeConfiguration<WorkRecord>
{
    public void Configure(EntityTypeBuilder<WorkRecord> builder)
    {
        builder.ToTable("WorkRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(record => record.Title).HasMaxLength(500).IsRequired();
        builder.Ignore(record => record.CurrentContent);
        builder.Ignore(record => record.IsDeleted);
        builder.HasQueryFilter(record => record.DeletedAtUtc == null);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(record => record.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(record => record.Versions)
            .WithOne()
            .HasForeignKey("WorkRecordId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(record => record.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(record => record.Attachments)
            .WithOne()
            .HasForeignKey("WorkRecordId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(record => record.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
