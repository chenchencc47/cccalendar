using CcCalendar.Core.Recurrence;
using CcCalendar.Infrastructure.Recurrence;

namespace CcCalendar.Infrastructure.Tests.Recurrence;

public sealed class IcalRecurrenceExpanderTests
{
    [Fact]
    public void ExpandRespectsDaysUntilExceptionsAndHolidays()
    {
        RecurrenceRule rule = RecurrenceRule.Create(
            RecurrenceFrequency.Weekly,
            1,
            [DayOfWeek.Monday, DayOfWeek.Wednesday],
            new DateOnly(2026, 8, 26),
            skipHolidays: true);
        var excludedDates = new HashSet<DateOnly> { new(2026, 8, 24) };
        var holidayCalendar = new FixedHolidayCalendar([new DateOnly(2026, 8, 19)]);
        var expander = new IcalRecurrenceExpander();

        IReadOnlyList<DateOnly> dates = expander.Expand(
            new DateOnly(2026, 8, 17),
            rule,
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 8, 31),
            excludedDates,
            holidayCalendar);

        Assert.Equal([new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 26)], dates);
    }

    [Fact]
    public void ExpandUsesCustomDailyInterval()
    {
        RecurrenceRule rule = RecurrenceRule.Create(
            RecurrenceFrequency.Daily,
            2,
            [],
            null,
            skipHolidays: false);
        var expander = new IcalRecurrenceExpander();

        IReadOnlyList<DateOnly> dates = expander.Expand(
            new DateOnly(2026, 8, 17),
            rule,
            new DateOnly(2026, 8, 18),
            new DateOnly(2026, 8, 24),
            new HashSet<DateOnly>(),
            new FixedHolidayCalendar([]));

        Assert.Equal([new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 23)], dates);
    }

    private sealed class FixedHolidayCalendar(IEnumerable<DateOnly> holidays) : IHolidayCalendar
    {
        private readonly HashSet<DateOnly> holidays = [.. holidays];

        public bool IsHoliday(DateOnly calendarDate) => holidays.Contains(calendarDate);
    }
}
