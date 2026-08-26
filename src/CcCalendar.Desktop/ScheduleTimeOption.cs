using System.Globalization;

namespace CcCalendar.Desktop;

public sealed record ScheduleTimeOption(TimeOnly Value, string Label)
{
    public static IReadOnlyList<ScheduleTimeOption> CreateHalfHourOptions()
    {
        return [.. Enumerable.Range(0, 48).Select(index =>
        {
            TimeOnly value = TimeOnly.MinValue.AddMinutes(index * 30);
            return new ScheduleTimeOption(
                value,
                value.ToString("HH:mm", CultureInfo.InvariantCulture));
        })];
    }

    public static TimeOnly RoundUpToHalfHour(TimeOnly value)
    {
        int minutes = value.Hour * 60 + value.Minute;
        int rounded = (minutes + 29) / 30 * 30;
        rounded = Math.Min(rounded, 22 * 60 + 30);
        return TimeOnly.MinValue.AddMinutes(rounded);
    }

    public static TimeOnly ParseExactMinute(string text)
    {
        if (!TimeOnly.TryParseExact(
                text?.Trim(),
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out TimeOnly result))
        {
            throw new ArgumentException("时间格式应为 HH:mm，例如 14:05。");
        }

        return result;
    }
}
