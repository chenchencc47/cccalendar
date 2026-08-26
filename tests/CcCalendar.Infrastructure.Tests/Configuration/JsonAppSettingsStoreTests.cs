using CcCalendar.Core.Configuration;
using CcCalendar.Infrastructure.Configuration;

namespace CcCalendar.Infrastructure.Tests.Configuration;

public sealed class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string testDirectory = Path.Combine(
        Path.GetTempPath(),
        "cccalendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadWhenFileIsMissingReturnsDefaults()
    {
        var store = CreateStore();

        AppSettings settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal("zh-CN", settings.Culture);
        Assert.Equal(DesktopCalendarMode.Month, settings.DesktopCalendar.Mode);
        Assert.Equal(680, settings.DesktopCalendar.MonthWidth);
    }

    [Fact]
    public async Task LoadWhenFileIsCorruptBacksItUpAndReturnsDefaults()
    {
        string settingsPath = Path.Combine(testDirectory, "settings.json");
        Directory.CreateDirectory(testDirectory);
        await File.WriteAllTextAsync(settingsPath, "{bad", CancellationToken.None);
        var store = new JsonAppSettingsStore(settingsPath);

        AppSettings settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.False(File.Exists(settingsPath));
        string[] backups = Directory.GetFiles(testDirectory, "settings.json.corrupt-*.json");
        string backup = Assert.Single(backups);
        Assert.Equal("{bad", await File.ReadAllTextAsync(backup, CancellationToken.None));
    }

    [Fact]
    public async Task ModelCatalogAndSelectionSurviveReload()
    {
        var store = CreateStore();
        var model = new AiModelConfiguration(
            "work-openai",
            "工作 OpenAI",
            new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiResponses,
                Endpoint = "https://example.test/v1",
                Model = "gpt-work",
            });
        var expected = new AppSettings
        {
            AiProvider = model.Settings,
            AiModels = [model],
            SelectedAiModelId = model.Id,
        };

        await store.SaveAsync(expected, CancellationToken.None);
        AppSettings actual = await store.LoadAsync(CancellationToken.None);

        AiModelConfiguration restored = Assert.Single(actual.AiModels);
        Assert.Equal(model, restored);
        Assert.Equal(model.Id, actual.SelectedAiModelId);
    }

    [Fact]
    public async Task SaveThenLoadReturnsSavedSettings()
    {
        var store = CreateStore();
        var expected = new AppSettings
        {
            Theme = ThemePreference.Dark,
            Culture = "zh-CN",
            StartWithMainWindowHidden = true,
            ShowDesktopCalendar = true,
            ShowDesktopAgenda = true,
            ShowDesktopTodo = false,
            DesktopCalendar = new DesktopCalendarLayoutSettings
            {
                Mode = DesktopCalendarMode.Week,
                MonthWidth = 720,
                MonthHeight = 500,
                WeekWidth = 650,
                WeekHeight = 300,
            },
            DesktopAgendaLayout = new DesktopPanelLayoutSettings
            {
                Width = 480,
                Height = 260,
                Left = 900,
                Top = 40,
            },
            DesktopTodoLayout = new DesktopPanelLayoutSettings
            {
                Width = 360,
                Height = 420,
                Left = 1180,
                Top = 360,
            },
            DesktopWorkbenchBehavior = new DesktopWindowBehaviorSettings
            {
                IsPositionLocked = true,
                IsMousePassthrough = true,
                Layer = DesktopWindowLayer.Topmost,
                IsEdgeAutoHideEnabled = true,
            },
            DesktopCalendarAppearance = new DesktopAppearanceSettings
            {
                Theme = ThemePreference.Dark,
                Material = DesktopBackgroundMaterial.Acrylic,
                Opacity = 0.72,
                CornerRadius = 10,
                HolidayTextColor = "#D93330",
                UseCustomColors = true,
            },
            GlobalShortcuts = new GlobalShortcutSettings
            {
                QuickAdd = new ShortcutGestureSettings
                {
                    Control = true,
                    Alt = true,
                    Key = "F9",
                },
            },
            ReminderNotifications = new ReminderNotificationSettings
            {
                IsSystemNotificationEnabled = false,
                IsPopupEnabled = true,
                IsSoundEnabled = false,
                DefaultSnoozeMinutes = 30,
                IsDoNotDisturbEnabled = true,
                DoNotDisturbStart = new TimeOnly(21, 30),
                DoNotDisturbEnd = new TimeOnly(7, 30),
                SuppressWhenFullscreen = true,
            },
            WeatherLocation = new WeatherLocationSettings
            {
                UseAutomaticLocation = false,
                ManualLocation = new SavedWeatherLocation
                {
                    Name = "上海",
                    Region = "上海",
                    Country = "中国",
                    Latitude = 31.23,
                    Longitude = 121.47,
                    TimeZoneId = "Asia/Shanghai",
                },
            },
            AiProvider = new AiProviderSettings
            {
                Provider = AiProviderKind.Ollama,
                Endpoint = "http://localhost:11434",
                Model = "qwen3:8b",
                LocalOnlyMode = true,
            },
            WellnessReminders = new WellnessReminderSettings
            {
                HourlyChimeEnabled = true,
                SedentaryReminderEnabled = true,
                SedentaryIntervalMinutes = 45,
                ActiveStart = new TimeOnly(9, 0),
                ActiveEnd = new TimeOnly(18, 0),
            },
        };

        await store.SaveAsync(expected, CancellationToken.None);
        AppSettings actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equivalent(expected, actual, strict: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private JsonAppSettingsStore CreateStore()
    {
        string settingsPath = Path.Combine(testDirectory, "settings.json");
        return new JsonAppSettingsStore(settingsPath);
    }
}
