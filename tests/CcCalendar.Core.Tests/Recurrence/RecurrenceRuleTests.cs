using CcCalendar.Core.Recurrence;

namespace CcCalendar.Core.Tests.Recurrence;

public sealed class RecurrenceRuleTests
{
    [Fact]
    public void CreateNormalizesDaysAndInterval()
    {
        RecurrenceRule rule = RecurrenceRule.Create(
            RecurrenceFrequency.Weekly,
            2,
            [DayOfWeek.Wednesday, DayOfWeek.Monday, DayOfWeek.Monday],
            new DateOnly(2026, 12, 31),
            skipHolidays: false);

        Assert.Equal(2, rule.Interval);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday], rule.DaysOfWeek);
    }

    [Fact]
    public void WorkdaysUsesMondayThroughFridayAndSkipsHolidays()
    {
        RecurrenceRule rule = RecurrenceRule.Workdays(null);

        Assert.Equal(RecurrenceFrequency.Daily, rule.Frequency);
        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            rule.DaysOfWeek);
        Assert.True(rule.SkipHolidays);
    }

    [Fact]
    public void CreateRejectsNonPositiveInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecurrenceRule.Create(
            RecurrenceFrequency.Daily,
            0,
            [],
            null,
            skipHolidays: false));
    }
}
