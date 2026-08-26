using CcCalendar.Core.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class ReminderSettingsViewModel : ObservableObject
{
    private readonly Action<ReminderNotificationSettings> settingsChanged;
    private ReminderNotificationSettings settings;

    public ReminderSettingsViewModel(
        ReminderNotificationSettings settings,
        Action<ReminderNotificationSettings>? settingsChanged = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        this.settingsChanged = settingsChanged ?? (_ => { });
        SnoozeMinuteOptions = [5, 10, 30, 60];
        TimeOptions = [.. Enumerable.Range(0, 48).Select(index => new TimeOnly(index / 2, index % 2 * 30))];
    }

    public IReadOnlyList<int> SnoozeMinuteOptions { get; }

    public IReadOnlyList<TimeOnly> TimeOptions { get; }

    public bool IsSystemNotificationEnabled
    {
        get => settings.IsSystemNotificationEnabled;
        set => Update(settings with { IsSystemNotificationEnabled = value });
    }

    public bool IsPopupEnabled
    {
        get => settings.IsPopupEnabled;
        set => Update(settings with { IsPopupEnabled = value });
    }

    public bool IsSoundEnabled
    {
        get => settings.IsSoundEnabled;
        set => Update(settings with { IsSoundEnabled = value });
    }

    public int DefaultSnoozeMinutes
    {
        get => settings.DefaultSnoozeMinutes;
        set => Update(settings with { DefaultSnoozeMinutes = Math.Clamp(value, 1, 1440) });
    }

    public bool IsDoNotDisturbEnabled
    {
        get => settings.IsDoNotDisturbEnabled;
        set => Update(settings with { IsDoNotDisturbEnabled = value });
    }

    public TimeOnly DoNotDisturbStart
    {
        get => settings.DoNotDisturbStart;
        set => Update(settings with { DoNotDisturbStart = value });
    }

    public TimeOnly DoNotDisturbEnd
    {
        get => settings.DoNotDisturbEnd;
        set => Update(settings with { DoNotDisturbEnd = value });
    }

    public bool SuppressWhenFullscreen
    {
        get => settings.SuppressWhenFullscreen;
        set => Update(settings with { SuppressWhenFullscreen = value });
    }

    public ReminderNotificationSettings Settings => settings;

    private void Update(ReminderNotificationSettings value)
    {
        if (settings == value)
        {
            return;
        }

        settings = value;
        settingsChanged(value);
        OnPropertyChanged(string.Empty);
    }
}
