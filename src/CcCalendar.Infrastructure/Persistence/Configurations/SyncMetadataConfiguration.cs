using CcCalendar.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class SyncMetadataConfiguration : IEntityTypeConfiguration<SyncMetadata>
{
    public void Configure(EntityTypeBuilder<SyncMetadata> builder)
    {
        builder.ToTable("SyncMetadata");
        builder.HasKey(metadata => metadata.Id);
        builder.Property(metadata => metadata.DeviceId).IsRequired();
        builder.HasIndex(metadata => metadata.DeviceId).IsUnique();
        builder.Property(metadata => metadata.PullCursor).HasMaxLength(500);
        builder.Property(metadata => metadata.UpdatedAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
    }
}
