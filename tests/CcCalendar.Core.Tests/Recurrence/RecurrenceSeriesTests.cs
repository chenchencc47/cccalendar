using CcCalendar.Core.Recurrence;

namespace CcCalendar.Core.Tests.Recurrence;

public sealed class RecurrenceSeriesTests
{
    [Fact]
    public void CreateCopiesRuleAndAssociatesCalendarEvent()
    {
        Guid calendarEventId = Guid.NewGuid();
        RecurrenceRule rule = RecurrenceRule.Create(
            RecurrenceFrequency.Weekly,
            2,
            [DayOfWeek.Monday, DayOfWeek.Wednesday],
            new DateOnly(2026, 12, 31),
            skipHolidays: true);

        RecurrenceSeries series = RecurrenceSeries.Create(calendarEventId, rule);

        Assert.Equal(calendarEventId, series.CalendarEventId);
        Assert.Equal(rule.Frequency, series.Rule.Frequency);
        Assert.Equal(rule.Interval, series.Rule.Interval);
        Assert.Equal(rule.DaysOfWeek, series.Rule.DaysOfWeek);
        Assert.True(series.Rule.SkipHolidays);
    }

    [Fact]
    public void ExcludeAddsEachDateOnce()
    {
        RecurrenceSeries series = RecurrenceSeries.Create(
            Guid.NewGuid(),
            RecurrenceRule.Workdays(null));
        var excludedDate = new DateOnly(2026, 8, 19);

        series.Exclude(excludedDate);
        series.Exclude(excludedDate);

        Assert.Single(series.ExcludedDates, excludedDate);
    }
}
