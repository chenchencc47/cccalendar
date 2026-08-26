using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class WeeklyRecurrencePlannerTests
{
    [Fact]
    public void GetDatesInRangeExpandsWeekdaysWithinInclusiveRange()
    {
        // 2026-08-20 是周四：范围内的周一 8/24、周二 8/25。
        IReadOnlyList<DateOnly> dates = WeeklyRecurrencePlanner.GetDatesInRange(
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 25),
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);

        Assert.Equal(
            [
                new DateOnly(2026, 8, 24),
                new DateOnly(2026, 8, 25),
            ],
            dates);
    }

    [Fact]
    public void GetDatesInRangeStartsFromRangeStartNotWeekOfAnchor()
    {
        // 范围内第一个周一是 8/24（早于 8/20 的 8/17 周一不算）。
        IReadOnlyList<DateOnly> dates = WeeklyRecurrencePlanner.GetDatesInRange(
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 31),
            [DayOfWeek.Monday]);

        Assert.Equal(
            [
                new DateOnly(2026, 8, 24),
                new DateOnly(2026, 8, 31),
            ],
            dates);
    }

    [Fact]
    public void GetDatesInRangeCoversLongRangeBoundaries()
    {
        // 用户示例：2026-08-20 到 2027-03-01（恰为周一），每周一重复。
        IReadOnlyList<DateOnly> dates = WeeklyRecurrencePlanner.GetDatesInRange(
            new DateOnly(2026, 8, 20),
            new DateOnly(2027, 3, 1),
            [DayOfWeek.Monday]);

        Assert.Equal(new DateOnly(2026, 8, 24), dates[0]);
        Assert.Equal(new DateOnly(2027, 3, 1), dates[^1]);
        Assert.Equal(28, dates.Count);
    }

    [Fact]
    public void GetDatesInRangeReturnsEmptyWhenNoWeekdayMatches()
    {
        // 2026-08-20（周四）到 8/23（周日）之间没有周一/周二。
        IReadOnlyList<DateOnly> dates = WeeklyRecurrencePlanner.GetDatesInRange(
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 23),
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);

        Assert.Empty(dates);
    }

    [Fact]
    public void GetDatesInRangeReturnsEmptyWhenEndBeforeStart()
    {
        IReadOnlyList<DateOnly> dates = WeeklyRecurrencePlanner.GetDatesInRange(
            new DateOnly(2026, 8, 25),
            new DateOnly(2026, 8, 20),
            [DayOfWeek.Monday]);

        Assert.Empty(dates);
    }

    [Fact]
    public void GetDatesInRangeDeduplicatesWeekdays()
    {
        IReadOnlyList<DateOnly> dates = WeeklyRecurrencePlanner.GetDatesInRange(
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 26),
            [DayOfWeek.Tuesday, DayOfWeek.Tuesday]);

        Assert.Equal([new DateOnly(2026, 8, 25)], dates);
    }
}
