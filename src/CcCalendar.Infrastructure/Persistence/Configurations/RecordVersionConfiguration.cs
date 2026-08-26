using CcCalendar.Core.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class RecordVersionConfiguration : IEntityTypeConfiguration<RecordVersion>
{
    public void Configure(EntityTypeBuilder<RecordVersion> builder)
    {
        builder.ToTable("RecordVersions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Content).IsRequired();
        builder.HasIndex("WorkRecordId", nameof(RecordVersion.VersionNumber)).IsUnique();
    }
}
