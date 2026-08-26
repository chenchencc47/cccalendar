using CcCalendar.Core.Tools;

namespace CcCalendar.Core.Tests.Tools;

public sealed class TimeUtilityTests
{
    [Fact]
    public void ProgressUsesLocalCalendarBoundaries()
    {
        var localNow = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.FromHours(8));

        TimeProgress progress = TimeProgressCalculator.Calculate(localNow);

        Assert.Equal(50, progress.DayPercent, 6);
        Assert.Equal(16.5 / 31 * 100, progress.MonthPercent, 6);
        Assert.InRange(progress.WeekPercent, 7.14, 7.15);
    }

    [Fact]
    public void DateCalculationAndWorldClockAreDeterministic()
    {
        DateOnly start = new(2026, 8, 16);
        DateOnly end = new(2026, 9, 1);
        var instant = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);

        int days = DateCalculator.DaysBetween(start, end);
        DateOnly added = DateCalculator.AddDays(start, 10);
        DateTimeOffset shanghai = WorldClock.Convert(
            instant,
            TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));

        Assert.Equal(16, days);
        Assert.Equal(new DateOnly(2026, 8, 26), added);
        Assert.Equal(8, shanghai.Hour);
        Assert.Equal(TimeSpan.FromHours(8), shanghai.Offset);
    }
}
