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

    // P70-06a：助理页左侧会话栏收起状态（序列化为 assistantSessionsCollapsed）。
    public bool AssistantSessionsCollapsed { get; init; }

    // P70-06b：主界面左侧导航栏收起状态（序列化为 mainNavCollapsed），false=展开 200px，true=rail 48px。
    public bool MainNavCollapsed { get; init; }

    // P70-06b：主界面顶部搜索栏收起状态（序列化为 topSearchCollapsed），收起后原位只留搜索图标按钮。
    public bool TopSearchCollapsed { get; init; }

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

    // P71-01：AI 助手"本地代理"档设置（序列化为 localAgent），默认关。
    public LocalAgentSettings LocalAgent { get; init; } = new();

    public IReadOnlyList<AiModelConfiguration> AiModels { get; init; } = [];

    public string? SelectedAiModelId { get; init; }

    // P74-04：视觉桥（图片附件 → 视觉模型转写）使用的模型 Id；null = 未启用。
    public string? VisualBridgeModelId { get; init; }

    public WellnessReminderSettings WellnessReminders { get; init; } = new();

    public TeamConnectionSettings TeamConnection { get; init; } = new();
}
