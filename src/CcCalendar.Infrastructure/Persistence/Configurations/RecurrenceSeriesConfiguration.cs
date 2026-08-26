using CcCalendar.Core.Recurrence;
using CcCalendar.Core.Schedules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class RecurrenceSeriesConfiguration : IEntityTypeConfiguration<RecurrenceSeries>
{
    public void Configure(EntityTypeBuilder<RecurrenceSeries> builder)
    {
        builder.ToTable("RecurrenceSeries");
        builder.HasKey(series => series.Id);
        builder.Property(series => series.Frequency).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Ignore(series => series.Rule);
        builder.Ignore(series => series.ExcludedDates);

        builder.HasOne<CalendarEvent>()
            .WithOne()
            .HasForeignKey<RecurrenceSeries>(series => series.CalendarEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(series => series.Exceptions)
            .WithOne()
            .HasForeignKey("RecurrenceSeriesId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(series => series.Exceptions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
