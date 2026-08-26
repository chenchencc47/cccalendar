using CcCalendar.Core.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class ReminderDeliveryConfiguration : IEntityTypeConfiguration<ReminderDelivery>
{
    public void Configure(EntityTypeBuilder<ReminderDelivery> builder)
    {
        builder.ToTable("ReminderDeliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Title).HasMaxLength(500).IsRequired();
        builder.Property(delivery => delivery.TargetType).HasConversion<string>().HasMaxLength(40);
        builder.Property(delivery => delivery.TriggerAtUtc).HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(delivery => delivery.DispatchedAtUtc).HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(delivery => delivery.AcknowledgedAtUtc).HasConversion<NullableUtcDateTimeOffsetConverter>();
        builder.Property(delivery => delivery.SnoozedUntilUtc).HasConversion<NullableUtcDateTimeOffsetConverter>();
        builder.HasIndex(delivery => delivery.ReminderId).IsUnique();
        builder.HasIndex(delivery => new
        {
            delivery.AcknowledgedAtUtc,
            delivery.SnoozedUntilUtc,
            delivery.DispatchedAtUtc,
        });
        builder.HasOne<Reminder>()
            .WithOne()
            .HasForeignKey<ReminderDelivery>(delivery => delivery.ReminderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
