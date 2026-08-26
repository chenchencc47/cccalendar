using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.Tools;

public enum WellnessReminderKind
{
    HourlyChime,
    Sedentary,
}

public sealed class WellnessReminderScheduler
{
    private DateTimeOffset lastActivityAt;
    private DateTimeOffset? lastChimeHour;
    private DateTimeOffset? lastSedentaryAt;

    public WellnessReminderScheduler(DateTimeOffset startedAt)
    {
        lastActivityAt = startedAt;
    }

    public IReadOnlyList<WellnessReminderKind> Evaluate(
        DateTimeOffset localNow,
        WellnessReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsActive(localNow.TimeOfDay, settings.ActiveStart, settings.ActiveEnd))
        {
            return [];
        }

        var reminders = new List<WellnessReminderKind>();
        DateTimeOffset hour = new(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            localNow.Hour,
            0,
            0,
            localNow.Offset);
        if (settings.HourlyChimeEnabled
            && localNow.Minute == 0
            && lastChimeHour != hour)
        {
            reminders.Add(WellnessReminderKind.HourlyChime);
            lastChimeHour = hour;
        }

        int intervalMinutes = Math.Clamp(settings.SedentaryIntervalMinutes, 15, 240);
        DateTimeOffset sedentaryBaseline = lastSedentaryAt > lastActivityAt
            ? lastSedentaryAt.Value
            : lastActivityAt;
        if (settings.SedentaryReminderEnabled
            && localNow - sedentaryBaseline >= TimeSpan.FromMinutes(intervalMinutes))
        {
            reminders.Add(WellnessReminderKind.Sedentary);
            lastSedentaryAt = localNow;
        }

        return reminders;
    }

    public void RecordActivity(DateTimeOffset localNow)
    {
        lastActivityAt = localNow;
        lastSedentaryAt = null;
    }

    private static bool IsActive(TimeSpan now, TimeOnly start, TimeOnly end)
    {
        TimeSpan startTime = start.ToTimeSpan();
        TimeSpan endTime = end.ToTimeSpan();
        return startTime <= endTime
            ? now >= startTime && now < endTime
            : now >= startTime || now < endTime;
    }
}
