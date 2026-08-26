namespace CcCalendar.Core.Configuration;

public sealed record ReminderNotificationSettings
{
    public bool IsSystemNotificationEnabled { get; init; } = true;

    public bool IsPopupEnabled { get; init; } = true;

    public bool IsSoundEnabled { get; init; } = true;

    public int DefaultSnoozeMinutes { get; init; } = 10;

    public bool IsDoNotDisturbEnabled { get; init; }

    public TimeOnly DoNotDisturbStart { get; init; } = new(22, 0);

    public TimeOnly DoNotDisturbEnd { get; init; } = new(8, 0);

    public bool SuppressWhenFullscreen { get; init; } = true;
}
