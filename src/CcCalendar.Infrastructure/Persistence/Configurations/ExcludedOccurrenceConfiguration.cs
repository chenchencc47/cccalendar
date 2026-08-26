using CcCalendar.Core.Recurrence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class ExcludedOccurrenceConfiguration : IEntityTypeConfiguration<ExcludedOccurrence>
{
    public void Configure(EntityTypeBuilder<ExcludedOccurrence> builder)
    {
        builder.ToTable("ExcludedOccurrences");
        builder.HasKey(exception => exception.Id);
        builder.HasIndex("RecurrenceSeriesId", nameof(ExcludedOccurrence.OccurrenceDate)).IsUnique();
    }
}
