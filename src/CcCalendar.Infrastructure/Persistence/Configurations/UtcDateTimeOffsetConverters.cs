using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CcCalendar.Infrastructure.Persistence.Configurations;

internal sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, long>
{
    public UtcDateTimeOffsetConverter()
        : base(
            value => value.UtcTicks,
            value => new DateTimeOffset(value, TimeSpan.Zero))
    {
    }
}

internal sealed class NullableUtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, long?>
{
    public NullableUtcDateTimeOffsetConverter()
        : base(
            value => value.HasValue ? value.Value.UtcTicks : null,
            value => value.HasValue
                ? new DateTimeOffset(value.Value, TimeSpan.Zero)
                : null)
    {
    }
}
