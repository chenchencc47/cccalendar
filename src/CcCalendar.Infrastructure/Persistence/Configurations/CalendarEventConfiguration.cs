using CcCalendar.Core.Projects;
using CcCalendar.Core.Schedules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("CalendarEvents");
        builder.HasKey(calendarEvent => calendarEvent.Id);
        builder.Property(calendarEvent => calendarEvent.Title).HasMaxLength(500).IsRequired();
        builder.Property(calendarEvent => calendarEvent.TimeZoneId).HasMaxLength(200);
        builder.Property(calendarEvent => calendarEvent.Location).HasMaxLength(200);
        builder.Property(calendarEvent => calendarEvent.MeetingInvitationText).HasMaxLength(20000);
        builder.Property(calendarEvent => calendarEvent.ReminderLeadMinutes);
        builder.Ignore(calendarEvent => calendarEvent.Duration);
        builder.Ignore(calendarEvent => calendarEvent.AllDayLength);
        builder.Ignore(calendarEvent => calendarEvent.IsDeleted);
        builder.HasQueryFilter(calendarEvent => calendarEvent.DeletedAtUtc == null);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
