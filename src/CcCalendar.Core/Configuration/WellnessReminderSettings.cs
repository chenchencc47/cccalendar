namespace CcCalendar.Core.Configuration;

public sealed record WellnessReminderSettings
{
    public bool HourlyChimeEnabled { get; init; }

    public bool SedentaryReminderEnabled { get; init; }

    public int SedentaryIntervalMinutes { get; init; } = 60;

    public TimeOnly ActiveStart { get; init; } = new(8, 0);

    public TimeOnly ActiveEnd { get; init; } = new(18, 0);
}
