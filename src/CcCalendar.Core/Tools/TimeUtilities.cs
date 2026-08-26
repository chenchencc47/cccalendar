namespace CcCalendar.Core.Tools;

public sealed record TimeProgress(
    double DayPercent,
    double WeekPercent,
    double MonthPercent,
    double YearPercent);

public static class TimeProgressCalculator
{
    public static TimeProgress Calculate(DateTimeOffset localNow)
    {
        DateTime local = localNow.DateTime;
        DateTime dayStart = local.Date;
        int daysSinceMonday = ((int)local.DayOfWeek + 6) % 7;
        DateTime weekStart = dayStart.AddDays(-daysSinceMonday);
        DateTime monthStart = new(local.Year, local.Month, 1);
        DateTime yearStart = new(local.Year, 1, 1);
        return new TimeProgress(
            Percent(local, dayStart, dayStart.AddDays(1)),
            Percent(local, weekStart, weekStart.AddDays(7)),
            Percent(local, monthStart, monthStart.AddMonths(1)),
            Percent(local, yearStart, yearStart.AddYears(1)));
    }

    private static double Percent(DateTime value, DateTime start, DateTime end)
    {
        return Math.Clamp((value - start) / (end - start) * 100, 0, 100);
    }
}

public static class DateCalculator
{
    public static int DaysBetween(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber;

    public static DateOnly AddDays(DateOnly date, int days) => date.AddDays(days);
}

public static class WorldClock
{
    public static DateTimeOffset Convert(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        return TimeZoneInfo.ConvertTime(instant, timeZone);
    }
}
