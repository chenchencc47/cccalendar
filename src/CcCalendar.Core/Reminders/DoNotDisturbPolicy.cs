using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.Reminders;

public static class DoNotDisturbPolicy
{
    public static bool IsSuppressed(
        DateTimeOffset localNow,
        ReminderNotificationSettings settings,
        bool isFullscreen)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SuppressWhenFullscreen && isFullscreen)
        {
            return true;
        }

        if (!settings.IsDoNotDisturbEnabled
            || settings.DoNotDisturbStart == settings.DoNotDisturbEnd)
        {
            return false;
        }

        TimeOnly current = TimeOnly.FromDateTime(localNow.DateTime);
        return settings.DoNotDisturbStart < settings.DoNotDisturbEnd
            ? current >= settings.DoNotDisturbStart && current < settings.DoNotDisturbEnd
            : current >= settings.DoNotDisturbStart || current < settings.DoNotDisturbEnd;
    }
}
