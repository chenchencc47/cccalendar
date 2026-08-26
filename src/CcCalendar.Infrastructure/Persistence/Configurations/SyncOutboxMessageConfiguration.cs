using CcCalendar.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class SyncOutboxMessageConfiguration : IEntityTypeConfiguration<SyncOutboxMessage>
{
    public void Configure(EntityTypeBuilder<SyncOutboxMessage> builder)
    {
        builder.ToTable("SyncOutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Operation)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(message => message.PayloadJson).IsRequired();
        builder.Property(message => message.CreatedAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(message => message.LastAttemptAtUtc)
            .HasConversion<NullableUtcDateTimeOffsetConverter>();
        builder.Property(message => message.AcknowledgedAtUtc)
            .HasConversion<NullableUtcDateTimeOffsetConverter>();
        builder.HasIndex(message => new { message.AcknowledgedAtUtc, message.CreatedAtUtc });
        builder.HasIndex(message => new { message.EntityType, message.EntityId });
    }
}
