namespace CcCalendar.Core.Configuration;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public string Culture { get; init; } = "zh-CN";

    public bool StartWithMainWindowHidden { get; init; }

    public bool ShowDesktopCalendar { get; init; } = true;

    public bool ShowDesktopAgenda { get; init; }

    public bool ShowDesktopTodo { get; init; }

    public DesktopCalendarLayoutSettings DesktopWorkbenchCalendar { get; init; } = new()
    {
        MonthWidth = 1000,
        MonthHeight = 520,
        WeekWidth = 1000,
        WeekHeight = 360,
    };

    public DesktopCalendarLayoutSettings DesktopCalendar { get; init; } = new();

    public DesktopPanelLayoutSettings DesktopAgendaLayout { get; init; } = new();

    public DesktopPanelLayoutSettings DesktopTodoLayout { get; init; } = new();

    public DesktopWindowBehaviorSettings DesktopWorkbenchBehavior { get; init; } = new();

    public DesktopWindowBehaviorSettings DesktopCalendarBehavior { get; init; } = new();

    public DesktopWindowBehaviorSettings DesktopAgendaBehavior { get; init; } = new();

    public DesktopWindowBehaviorSettings DesktopTodoBehavior { get; init; } = new();

    public DesktopAppearanceSettings DesktopWorkbenchAppearance { get; init; } = new();

    public DesktopAppearanceSettings DesktopCalendarAppearance { get; init; } = new();

    public DesktopAppearanceSettings DesktopAgendaAppearance { get; init; } = new();

    public DesktopAppearanceSettings DesktopTodoAppearance { get; init; } = new();

    public GlobalShortcutSettings GlobalShortcuts { get; init; } = new();

    public ReminderNotificationSettings ReminderNotifications { get; init; } = new();

    public WeatherLocationSettings WeatherLocation { get; init; } = new();

    public AiProviderSettings AiProvider { get; init; } = new();

    public IReadOnlyList<AiModelConfiguration> AiModels { get; init; } = [];

    public string? SelectedAiModelId { get; init; }

    public WellnessReminderSettings WellnessReminders { get; init; } = new();

    public TeamConnectionSettings TeamConnection { get; init; } = new();
}
