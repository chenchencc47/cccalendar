using CcCalendar.Core.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");
        builder.HasKey(reminder => reminder.Id);
        builder.Property(reminder => reminder.Title).HasMaxLength(500).IsRequired();
        builder.Property(reminder => reminder.TargetType).HasConversion<string>().HasMaxLength(40);
        builder.Property(reminder => reminder.TriggerAtUtc).HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(reminder => reminder.CreatedAtUtc).HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(reminder => reminder.QueuedAtUtc).HasConversion<NullableUtcDateTimeOffsetConverter>();
        builder.HasIndex(reminder => new
        {
            reminder.TargetType,
            reminder.TargetId,
            reminder.TriggerAtUtc,
        }).IsUnique();
        builder.HasIndex(reminder => new { reminder.QueuedAtUtc, reminder.TriggerAtUtc });
    }
}
