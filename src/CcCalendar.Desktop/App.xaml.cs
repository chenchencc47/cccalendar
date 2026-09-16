using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using CcCalendar.Core.Calendars;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Core.Rooms;
using CcCalendar.Core.Tools;
using CcCalendar.Desktop.Desktop;
using CcCalendar.Desktop.Reminders;
using CcCalendar.Desktop.Tools;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;
using CcCalendar.Desktop.Weather;
using CcCalendar.Infrastructure.AI;
using CcCalendar.Infrastructure.Calendars;
using CcCalendar.Infrastructure.Configuration;
using CcCalendar.Infrastructure.Reminders;
using CcCalendar.Infrastructure.Security;
using CcCalendar.Infrastructure.Sync;
using CcCalendar.Infrastructure.Updates;
using CcCalendar.Infrastructure.Weather;
using Microsoft.Win32;

namespace CcCalendar.Desktop;

public partial class App : Application, IDisposable
{
    // Set this to the public HTTPS version.json URL before a production build.
    // The environment variable remains an override for staging and rollback tests.
    private const string DefaultUpdateManifestUrl =
        "https://cccalendar-releases-01.oss-cn-heyuan.aliyuncs.com/releases/version.json";
    private const string SingleInstanceMutexName = "Local\\cccalendar-single-instance";

    private Mutex? singleInstanceMutex;
    private bool ownsSingleInstanceMutex;
    private MainWindow? mainWindow;
    private QuickPanelWindow? quickPanel;
    private DesktopWorkbenchWindow? desktopWorkbench;
    private DesktopComponentWindow? desktopAgenda;
    private DesktopComponentWindow? desktopTodo;
    private TrayIconService? trayIcon;
    private GlobalHotkeyManager? hotkeyManager;
    private HttpClient? weatherHttpClient;
    private HttpClient? aiHttpClient;
    private HttpClient? oidcHttpClient;
    private ReminderBackgroundScheduler? reminderScheduler;
    private ReminderNotificationHost? reminderNotificationHost;
    private PopupReminderChannel? popupReminderChannel;
    private WellnessReminderHost? wellnessReminderHost;
    private JsonAppSettingsStore? settingsStore;
    private AppSettings settings = new();
    private OidcTokenStore? teamRoomBoardTokenStore;
    private TeamRoomBoardService? teamRoomBoardService;
    private StoredTokenAccessTokenProvider? roomBookingTokenProvider;
    private HttpClient? roomBookingHttpClient;
    private HttpClient? updateHttpClient;
    private Uri? roomBookingBaseUri;
    private bool isExiting;
    private bool startupCompleted;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        singleInstanceMutex = new Mutex(
            true,
            SingleInstanceMutexName,
            out ownsSingleInstanceMutex);
        if (!ownsSingleInstanceMutex)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            await InitializeStartupAsync();
        }
        catch (Exception ex)
        {
            HandleStartupFailure(ex);
        }
    }

    private async Task InitializeStartupAsync()
    {
        settingsStore = new JsonAppSettingsStore(ApplicationPaths.SettingsPath);
        settings = await settingsStore.LoadAsync(CancellationToken.None);
        ApplyTheme(settings.Theme);
        var reminderService = new ReminderDispatchService(ApplicationPaths.DatabasePath);
        await reminderService.InitializeAsync(CancellationToken.None);
        reminderScheduler = new ReminderBackgroundScheduler(
            reminderService,
            TimeProvider.System,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1));
        reminderScheduler.Start();
        IChineseCalendarService chineseCalendarService = new LunarChineseCalendarService();
        weatherHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var weather = new WeatherViewModel(
            new OpenMeteoWeatherService(weatherHttpClient),
            new WindowsGeolocationProvider(),
            settings.WeatherLocation,
            updated => settings = settings with { WeatherLocation = updated });
        quickPanel = new QuickPanelWindow(chineseCalendarService, weather);
        var visibilityActions = new DesktopComponentVisibilityActions(
            ToggleDesktopComponent,
            IsDesktopComponentVisible);
        desktopWorkbench = new DesktopWorkbenchWindow(
            settings.DesktopWorkbenchCalendar,
            settings.DesktopWorkbenchBehavior,
            settings.DesktopWorkbenchAppearance,
            chineseCalendarService,
            CreateScheduleFromDesktopAsync,
            EditScheduleFromDesktopAsync,
            visibilityActions,
            DeleteScheduleFromDesktopAsync);
        desktopAgenda = new DesktopComponentWindow(
            DesktopComponentKind.Agenda,
            panelLayoutSettings: settings.DesktopAgendaLayout,
            behaviorSettings: settings.DesktopAgendaBehavior,
            appearanceSettings: settings.DesktopAgendaAppearance,
            chineseCalendarService: chineseCalendarService,
            scheduleRequested: CreateScheduleFromDesktopAsync,
            scheduleEditRequested: EditScheduleFromDesktopAsync,
            visibilityActions: visibilityActions,
            scheduleDeleteRequested: DeleteScheduleFromDesktopAsync);
        desktopTodo = new DesktopComponentWindow(
            DesktopComponentKind.Todo,
            panelLayoutSettings: settings.DesktopTodoLayout,
            behaviorSettings: settings.DesktopTodoBehavior,
            appearanceSettings: settings.DesktopTodoAppearance,
            chineseCalendarService: chineseCalendarService,
            scheduleRequested: CreateScheduleFromDesktopAsync,
            visibilityActions: visibilityActions,
            todoCompleteRequested: CompleteTodoFromDesktopAsync,
            todoCreationRequested: CreateTodoFromDesktopAsync);
        hotkeyManager = new GlobalHotkeyManager(
            new Dictionary<GlobalShortcutAction, Action>
            {
                [GlobalShortcutAction.OpenMainWindow] = ShowMainWindow,
                [GlobalShortcutAction.ToggleQuickPanel] = ToggleQuickPanel,
                [GlobalShortcutAction.QuickAdd] = OpenQuickAdd,
                [GlobalShortcutAction.ToggleDesktopWorkbench] = ToggleDesktopWorkbench,
                [GlobalShortcutAction.DisableMousePassthrough] = DisableAllMousePassthrough,
            });
        var shortcutSettings = new ShortcutSettingsViewModel(
            settings.GlobalShortcuts,
            hotkeyManager,
            updated => settings = settings with { GlobalShortcuts = updated });
        var reminderSettings = new ReminderSettingsViewModel(
            settings.ReminderNotifications,
            updated => settings = settings with { ReminderNotifications = updated });
        aiHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var secretStore = new WindowsCredentialSecretStore();
        IReadOnlyList<AiModelConfiguration> configuredModels = settings.AiModels.Count > 0
            ? settings.AiModels
            : [new AiModelConfiguration("legacy", settings.AiProvider.Model, settings.AiProvider)];
        AiModelCatalog? modelCatalog = null;
        modelCatalog = new AiModelCatalog(
            configuredModels,
            settings.SelectedAiModelId,
            settings.AiProvider,
            selectedId =>
            {
                settings = settings with
                {
                    AiModels = [.. modelCatalog!.Models],
                    SelectedAiModelId = selectedId,
                    AiProvider = modelCatalog.CurrentSettings,
                };
                SaveAppSettings();
            });
        settings = settings with
        {
            AiModels = [.. modelCatalog.Models],
            SelectedAiModelId = modelCatalog.SelectedModel?.Id,
            AiProvider = modelCatalog.CurrentSettings,
        };
        var aiSettings = new AiSettingsViewModel(
            modelCatalog,
            secretStore,
            new AiConnectionTester(aiHttpClient),
            SaveAiModelConfigurationsAsync);
        await aiSettings.LoadSecretStatusAsync(CancellationToken.None);
        oidcHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var oidcLoginClient = new OidcLoginClient(
            new OidcTokenClient(oidcHttpClient),
            new SystemBrowserLauncher());
        var devTokenLoginClient = new DevTokenLoginClient(oidcHttpClient);
        var teamConnection = new TeamConnectionViewModel(
            settings.TeamConnection,
            oidcLoginClient.LoginAsync,
            new OidcTokenStore(secretStore),
            updated =>
            {
                settings = settings with { TeamConnection = updated };
                // Persist team connection changes immediately so an installer restart
                // cannot lose the credentials needed for silent startup reconnect.
                SaveAppSettings();
            },
            (apiBaseUrl, name, sharedSecret, cancellationToken) =>
                devTokenLoginClient.LoginAsync(apiBaseUrl, name, sharedSecret, cancellationToken));
        teamRoomBoardTokenStore = new OidcTokenStore(secretStore);
        teamRoomBoardService = new TeamRoomBoardService(
            () => settings.TeamConnection,
            teamSettings => CreateRoomBookingClient(teamSettings));
        await teamConnection.RestoreAsync(CancellationToken.None);
        var aiAssistantClient = new ProviderAiAssistantClient(
            aiHttpClient,
            () => modelCatalog.CurrentSettings,
            secretStore,
            new SqliteReadOnlyAiToolExecutor(ApplicationPaths.DatabasePath));
        var appearanceSettings = new AppearanceSettingsViewModel(
            settings.DesktopWorkbenchAppearance,
            settings.DesktopCalendarAppearance,
            settings.DesktopAgendaAppearance,
            settings.DesktopTodoAppearance,
            ApplyDesktopAppearance,
            shortcutSettings,
            reminderSettings,
            aiSettings,
            settings.StartWithMainWindowHidden,
            updated => settings = settings with { StartWithMainWindowHidden = updated },
            settings.Theme,
            ApplyThemeFromSettings,
            teamConnection);
        var wellnessScheduler = new WellnessReminderScheduler(TimeProvider.System.GetLocalNow());
        mainWindow = new MainWindow(
            appearanceSettings,
            chineseCalendarService,
            weather,
            aiAssistantClient,
            modelCatalog,
            settings.WellnessReminders,
            updated => settings = settings with { WellnessReminders = updated },
            wellnessScheduler.RecordActivity,
            ReloadDesktopWindowsAsync,
            LoadTeamRoomBoardAsync,
            SubmitTeamBookingAsync,
            DeleteRemoteBookingAsync);
        teamConnection.ConnectionChanged += (_, _) =>
        {
            _ = mainWindow.RefreshOpenRoomBoardAsync();
        };
        DesktopBehaviorMenuTarget[] behaviorTargets =
        [
            new("桌面日历", desktopWorkbench),
            new("桌面日程", desktopAgenda),
            new("桌面待办", desktopTodo),
        ];
        trayIcon = new TrayIconService(
            ShowMainWindow,
            ToggleQuickPanel,
            () => ToggleDesktopComponent(DesktopComponentKind.Calendar),
            () => ToggleDesktopComponent(DesktopComponentKind.Agenda),
            () => ToggleDesktopComponent(DesktopComponentKind.Todo),
            behaviorTargets,
            DisableAllMousePassthrough,
            ExitApplication);
        mainWindow.ToolNotificationRequested += (_, notification) =>
        {
            trayIcon?.ShowNotification(notification.Title, notification.Message);
            System.Media.SystemSounds.Asterisk.Play();
        };
        wellnessReminderHost = new WellnessReminderHost(
            wellnessScheduler,
            TimeProvider.System,
            () => settings.WellnessReminders,
            trayIcon.ShowNotification);
        wellnessReminderHost.Start();
        ReminderNotificationCoordinator? notificationCoordinator = null;
        popupReminderChannel = new PopupReminderChannel(
            () => settings.ReminderNotifications.DefaultSnoozeMinutes,
            delivery => notificationCoordinator!.AcknowledgeAsync(
                delivery,
                CancellationToken.None),
            (delivery, duration) => notificationCoordinator!.SnoozeAsync(
                delivery,
                duration,
                CancellationToken.None));
        notificationCoordinator = new ReminderNotificationCoordinator(
            reminderService,
            TimeProvider.System,
            () => settings.ReminderNotifications,
            new WindowsFullscreenDetector(),
            new SystemTrayReminderChannel(trayIcon.ShowNotification),
            popupReminderChannel,
            new SystemSoundReminderChannel());
        reminderNotificationHost = new ReminderNotificationHost(
            notificationCoordinator,
            Dispatcher,
            TimeSpan.FromSeconds(5));
        reminderNotificationHost.Start();
        mainWindow.Closing += MainWindowClosing;
        MainWindow = mainWindow;
        if (!settings.StartWithMainWindowHidden)
        {
            mainWindow.Show();
        }

        if (settings.ShowDesktopCalendar)
        {
            desktopWorkbench.Show();
        }

        if (settings.ShowDesktopAgenda)
        {
            desktopAgenda.Toggle();
        }

        if (settings.ShowDesktopTodo)
        {
            desktopTodo.Toggle();
        }

        weather.RefreshCommand.Execute(null);
        startupCompleted = true;
        _ = CheckForUpdatesAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SaveAppSettings();
        Dispose();
        if (ownsSingleInstanceMutex)
        {
            singleInstanceMutex?.ReleaseMutex();
            ownsSingleInstanceMutex = false;
        }
        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;
        base.OnExit(e);
    }

    private void HandleStartupFailure(Exception ex)
    {
        LogCrash(ex);
        isExiting = true;
        MessageBox.Show(
            $"应用程序启动失败：\n\n{ex.Message}\n\n详细信息已记录到崩溃日志。",
            "cccalendar 启动错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(-1);
    }


    private void ApplyTheme(ThemePreference theme)
    {
        ThemePreference effective = theme;
        if (theme == ThemePreference.System)
        {
            using RegistryKey? personaliseKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            effective = personaliseKey?.GetValue("AppsUseLightTheme") as int? == 1
                ? ThemePreference.Light
                : ThemePreference.Dark;
        }

        // Remove existing dark theme dictionary if present
        for (int i = Resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (Resources.MergedDictionaries[i].Source?.OriginalString.Contains("DarkTheme") == true)
            {
                Resources.MergedDictionaries.RemoveAt(i);
            }
        }

        if (effective == ThemePreference.Dark)
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative),
            });
        }
    }

    private void ApplyThemeFromSettings(ThemePreference theme)
    {
        ApplyTheme(theme);
        settings = settings with { Theme = theme };
        SaveAppSettings();
    }
    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        if (sender is App app && !app.startupCompleted)
        {
            app.isExiting = true;
            e.Handled = true;
            app.Shutdown(-1);
            return;
        }

        MessageBox.Show(
            $"应用程序发生错误：\n\n{e.Exception.Message}\n\n详细信息已记录到崩溃日志。",
            "cccalendar 错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash(ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        e.SetObserved();
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "cccalendar");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "crash.log");
            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            string entry = $"[{timestamp}]\n{ex}\n\n";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            // Best effort logging
        }
    }

    public void Dispose()
    {
        wellnessReminderHost?.Dispose();
        wellnessReminderHost = null;
        reminderNotificationHost?.Dispose();
        reminderNotificationHost = null;
        popupReminderChannel?.Dispose();
        popupReminderChannel = null;
        if (reminderScheduler is not null)
        {
            try
            {
                reminderScheduler.DisposeAsync()
                    .AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(2))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (TimeoutException)
            {
                LogCrash(new TimeoutException("提醒调度器在退出时未能及时停止，应用将继续关闭。"));
            }
            catch (Exception exception)
            {
                LogCrash(exception);
            }

            reminderScheduler = null;
        }

        hotkeyManager?.Dispose();
        hotkeyManager = null;
        trayIcon?.Dispose();
        trayIcon = null;
        weatherHttpClient?.Dispose();
        weatherHttpClient = null;
        aiHttpClient?.Dispose();
        aiHttpClient = null;
        oidcHttpClient?.Dispose();
        oidcHttpClient = null;
        roomBookingHttpClient?.Dispose();
        roomBookingHttpClient = null;
        roomBookingBaseUri = null;
        updateHttpClient?.Dispose();
        updateHttpClient = null;
        mainWindow?.Dispose();
        mainWindow = null;
        GC.SuppressFinalize(this);
    }

    private void MainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (isExiting)
        {
            return;
        }

        e.Cancel = true;
        mainWindow?.Hide();
    }

    private void ShowMainWindow()
    {
        if (isExiting)
        {
            return;
        }

        mainWindow?.Show();
        mainWindow?.Activate();
    }

    /// <summary>
    /// 当前程序集版本（唯一来源）。
    ///
    /// 此前"设置 → 应用更新"那行文字把版本号**写死**成 `0.6.5`，发版时无人更新它，
    /// 更新到 0.6.7 后界面仍显示 0.6.5。改为统一从程序集读取：
    /// 版本号只由 `CcCalendar.Desktop.csproj` 的 `&lt;Version&gt;` 决定。
    /// </summary>
    internal static Version CurrentVersion
        => typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// 取回清单里的更新说明，供「设置 → 查看更新内容」展示。
    ///
    /// 走不做版本过滤的读取路径：远程通常已是最新一版，其 `releaseNotes` 正是用户
    /// 想看的当前版本说明。不可达时返回 <c>null</c>，由调用方给出明确提示。
    /// </summary>
    internal async Task<string?> FetchReleaseNotesAsync()
    {
        string? manifestText = Environment.GetEnvironmentVariable("CCCALENDAR_UPDATE_MANIFEST_URL");
        if (string.IsNullOrWhiteSpace(manifestText))
        {
            manifestText = DefaultUpdateManifestUrl;
        }

        if (!Uri.TryCreate(manifestText, UriKind.Absolute, out Uri? manifestUri)
            || !string.Equals(manifestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            updateHttpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var updateClient = new ApplicationUpdateClient(updateHttpClient);
            ApplicationUpdateManifest? manifest = await updateClient.FetchManifestAsync(
                manifestUri,
                CancellationToken.None);
            return manifest?.ReleaseNotes;
        }
        catch (Exception)
        {
            // 取说明失败不应影响任何本地功能。
            return null;
        }
    }

    internal async Task<UpdateCheckResult> CheckForUpdatesAsync(Window? owner = null)
    {
        string? manifestText = Environment.GetEnvironmentVariable("CCCALENDAR_UPDATE_MANIFEST_URL");
        if (string.IsNullOrWhiteSpace(manifestText))
        {
            manifestText = DefaultUpdateManifestUrl;
        }
        if (!Uri.TryCreate(manifestText, UriKind.Absolute, out Uri? manifestUri)
            || !string.Equals(manifestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || (owner is null && mainWindow is null))
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Skipped,
                CurrentVersion,
                Error: "更新地址未配置或不是 HTTPS 地址。");
        }

        Version currentVersion = CurrentVersion;
        try
        {
            updateHttpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var updateClient = new ApplicationUpdateClient(updateHttpClient);
            ApplicationUpdateManifest? update = await updateClient.CheckAsync(
                manifestUri,
                currentVersion,
                CancellationToken.None);
            if (update is null)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Latest, currentVersion);
            }

            // Checking for updates only surfaces the opt-in action; it never downloads
            // or installs anything until the user clicks the update button.
            mainWindow?.SetAvailableUpdate(update);
            return new UpdateCheckResult(UpdateCheckStatus.Available, currentVersion, update);
        }
        catch (Exception exception)
        {
            // Update checks are best-effort and must never prevent local calendar use.
            return new UpdateCheckResult(UpdateCheckStatus.Failed, currentVersion, Error: exception.Message);
        }
    }

    internal async Task InstallUpdateAsync(
        ApplicationUpdateManifest update,
        Window owner)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(owner);

        if (isExiting)
        {
            return;
        }

        try
        {
            string releaseNotes = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                ? "此版本暂无详细更新说明。"
                : update.ReleaseNotes.Trim();
            MessageBoxResult confirmation = MessageBox.Show(
                owner,
                $"新版本 {update.Version} 更新内容：\n\n{releaseNotes}\n\n点击“是”后，应用会下载并校验安装包，然后自动退出并启动安装程序。安装完成后会自动重新打开。是否继续？",
                "cccalendar 更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            updateHttpClient ??= new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            string updateDirectory = Path.Combine(Path.GetTempPath(), "cccalendar-update");
            string installerPath = await new ApplicationUpdateClient(updateHttpClient)
                .DownloadInstallerAsync(update, updateDirectory, CancellationToken.None);

            Process.Start(new ProcessStartInfo(installerPath)
            {
                Arguments = "/CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(installerPath),
            });
            ExitApplication();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                owner,
                $"下载或启动安装程序失败：{exception.Message}",
                "cccalendar 更新",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    internal enum UpdateCheckStatus
    {
        Skipped,
        Latest,
        Available,
        Failed,
    }

    internal sealed record UpdateCheckResult(
        UpdateCheckStatus Status,
        Version CurrentVersion,
        ApplicationUpdateManifest? Update = null,
        string? Error = null);

    private void ToggleQuickPanel()
    {
        if (isExiting)
        {
            return;
        }

        quickPanel?.Toggle();
    }

    private void OpenQuickAdd()
    {
        if (isExiting)
        {
            return;
        }

        mainWindow?.Show();
        mainWindow?.Activate();
        _ = mainWindow?.OpenQuickAddAsync() ?? Task.CompletedTask;
    }

    private void ExitApplication()
    {
        isExiting = true;
        trayIcon?.Hide();
        quickPanel?.Close();
        desktopWorkbench?.Close();
        desktopAgenda?.Close();
        desktopTodo?.Close();
        mainWindow?.Close();
        Shutdown();
    }

    private void ToggleDesktopWorkbench()
    {
        if (isExiting)
        {
            return;
        }

        ToggleDesktopComponent(DesktopComponentKind.Calendar);
    }

    private void DisableAllMousePassthrough()
    {
        if (isExiting)
        {
            return;
        }

        desktopWorkbench?.DisableMousePassthrough();
        desktopAgenda?.DisableMousePassthrough();
        desktopTodo?.DisableMousePassthrough();
    }

    private void ApplyDesktopAppearance(
        DesktopAppearanceTarget target,
        DesktopAppearanceSettings appearance)
    {
        IDesktopAppearanceTarget? window = target switch
        {
            DesktopAppearanceTarget.Workbench => desktopWorkbench,
            DesktopAppearanceTarget.Calendar => desktopWorkbench,
            DesktopAppearanceTarget.Agenda => desktopAgenda,
            DesktopAppearanceTarget.Todo => desktopTodo,
            _ => null,
        };
        window?.ApplyAppearance(appearance);
        settings = target switch
        {
            DesktopAppearanceTarget.Workbench => settings with
            {
                DesktopWorkbenchAppearance = appearance,
            },
            DesktopAppearanceTarget.Calendar => settings with
            {
                DesktopCalendarAppearance = appearance,
            },
            DesktopAppearanceTarget.Agenda => settings with
            {
                DesktopAgendaAppearance = appearance,
            },
            DesktopAppearanceTarget.Todo => settings with
            {
                DesktopTodoAppearance = appearance,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        SaveAppSettings();
    }

    private async Task SaveAiModelConfigurationsAsync(
        IReadOnlyList<AiModelConfiguration> models,
        string? selectedModelId,
        CancellationToken cancellationToken)
    {
        AiModelConfiguration? selected = models.FirstOrDefault(
            model => model.Id == selectedModelId);
        settings = settings with
        {
            AiModels = models,
            SelectedAiModelId = selectedModelId,
            AiProvider = selected?.Settings ?? settings.AiProvider,
        };
        await SaveAppSettingsAsync(cancellationToken);
    }

    private Task CreateScheduleFromDesktopAsync(DateOnly date, Window owner)
    {
        return mainWindow?.OpenScheduleForDateAsync(date, owner) ?? Task.CompletedTask;
    }

    private Task EditScheduleFromDesktopAsync(Guid eventId, Window owner)
    {
        return mainWindow?.OpenEventEditAsync(eventId, owner) ?? Task.CompletedTask;
    }

    /// <summary>按当前团队连接创建在线预约客户端；HttpClient 按服务端地址缓存复用。</summary>
    private RoomBookingApiClient CreateRoomBookingClient(TeamConnectionSettings teamSettings)
    {
        Uri baseUri = new Uri(teamSettings.ApiBaseUrl.Trim());
        if (roomBookingHttpClient is null || roomBookingBaseUri != baseUri)
        {
            roomBookingHttpClient?.Dispose();
            roomBookingHttpClient = new HttpClient
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(30),
            };
            roomBookingBaseUri = baseUri;
        }

        roomBookingTokenProvider ??= new StoredTokenAccessTokenProvider(
            teamRoomBoardTokenStore!,
            () => settings.TeamConnection.ResolveTokenIdentifier());
        return new RoomBookingApiClient(roomBookingHttpClient, roomBookingTokenProvider);
    }

    private async Task<TeamRoomBoardSnapshot?> LoadTeamRoomBoardAsync(DateOnly date, TimeZoneInfo zone)
    {
        TeamRoomBoardData? data = await teamRoomBoardService!.LoadAsync(
            date,
            zone,
            CancellationToken.None);
        if (data is not null)
        {
            int synced = await SyncLocalRoomEventsAsync(data, date, zone);
            if (synced > 0)
            {
                data = await teamRoomBoardService.LoadAsync(date, zone, CancellationToken.None);
            }
        }

        if (data is null)
        {
            return null;
        }

        Guid? currentUserId = Guid.TryParse(
            settings.TeamConnection.CurrentUserId,
            out Guid parsedUserId)
            ? parsedUserId
            : null;
        return TeamRoomBoardMapper.ToSnapshot(
            data,
            currentUserId,
            settings.TeamConnection.DevTokenName);
    }

    private async Task<int> SyncLocalRoomEventsAsync(
        TeamRoomBoardData data,
        DateOnly date,
        TimeZoneInfo zone)
    {
        int synced = 0;
        foreach (var calendarEvent in mainWindow!.GetEventsOnDate(date))
        {
            TeamRoomBookingSubmission? submission = TeamRoomBookingSubmissionResolver.TryCreate(
                new QuickAddRequest(
                    QuickAddKind.Event,
                    calendarEvent.Title,
                    calendarEvent.StartAtUtc,
                    calendarEvent.EndAtUtc,
                    calendarEvent.TimeZoneId,
                    Location: calendarEvent.Location,
                    MeetingInvitationText: calendarEvent.MeetingInvitationText),
                zone);
            if (submission is null)
            {
                continue;
            }

            RoomCatalogEntry? room = data.Rooms.FirstOrDefault(
                item => string.Equals(item.Name, submission.Room, StringComparison.Ordinal));
            if (room is null)
            {
                continue;
            }

            DateTimeOffset startUtc = TimeZoneInfo.ConvertTimeToUtc(
                submission.Date.ToDateTime(submission.Start),
                zone);
            DateTimeOffset endUtc = TimeZoneInfo.ConvertTimeToUtc(
                submission.Date.ToDateTime(submission.End),
                zone);
            bool alreadyBooked = data.Bookings.Any(booking =>
                booking.RoomId == room.Id
                && string.Equals(booking.Title, submission.Title, StringComparison.Ordinal)
                && booking.StartAtUtc == startUtc
                && booking.EndAtUtc == endUtc);
            if (alreadyBooked)
            {
                RoomBookingResult? remote = data.Bookings.FirstOrDefault(booking =>
                    booking.RoomId == room.Id
                    && string.Equals(booking.Title, submission.Title, StringComparison.Ordinal)
                    && booking.StartAtUtc == startUtc
                    && booking.EndAtUtc == endUtc);
                if (remote is not null
                    && string.IsNullOrWhiteSpace(remote.MeetingInvitationText)
                    && !string.IsNullOrWhiteSpace(submission.MeetingInvitationText)
                    && Guid.TryParse(settings.TeamConnection.CurrentUserId, out Guid currentUserId)
                    && remote.OrganizerId == currentUserId)
                {
                    try
                    {
                        await teamRoomBoardService!.TryUpdateBookingInvitationAsync(
                            remote.Id,
                            submission.MeetingInvitationText!,
                            CancellationToken.None);
                        synced++;
                    }
                    catch (HttpRequestException exception)
                        when (exception.StatusCode is System.Net.HttpStatusCode.Unauthorized
                            or System.Net.HttpStatusCode.Forbidden)
                    {
                        // Retry on a later refresh after the team token is repaired.
                    }
                }
                continue;
            }

            try
            {
                if (await teamRoomBoardService!.TryCreateBookingAsync(
                    submission.Room,
                    submission.Title,
                    submission.Date,
                    submission.Start,
                    submission.End,
                    zone,
                    CancellationToken.None,
                    $"calendar-event-{calendarEvent.Id:N}",
                    submission.MeetingInvitationText))
                {
                    synced++;
                }
            }
            catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // 其他成员已占用时保留本地日程，不阻断看板加载。
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode is System.Net.HttpStatusCode.Unauthorized
                    or System.Net.HttpStatusCode.Forbidden)
            {
                // 当前身份不能补交本地日程时，仍然保留已从云端加载的预约看板。
            }
        }

        return synced;
    }

    private Task<bool> SubmitTeamBookingAsync(
        string room,
        string title,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        string? meetingInvitationText = null)
    {
        return teamRoomBoardService!.TryCreateBookingAsync(
            room,
            title,
            date,
            start,
            end,
            TimeZoneInfo.Local,
            CancellationToken.None,
            meetingInvitationText: meetingInvitationText);
    }

    private async Task DeleteRemoteBookingAsync(RoomOccupiedBlock block)
    {
        if (!block.IsRemoteBooking
            || !block.IsOwnedByCurrentUser
            || block.EventId is not Guid bookingId)
        {
            throw new InvalidOperationException("只能删除自己创建的云端预约。");
        }

        RoomBookingApiClient client = CreateRoomBookingClient(settings.TeamConnection);
        await client.CancelBookingAsync(bookingId, CancellationToken.None);
        // The cloud booking and its local mirror have different IDs. Remove the
        // local mirror as well, otherwise the next refresh renders it again and
        // the background sync can recreate the deleted cloud booking.
        await mainWindow!.DeleteLocalEventsForRemoteBookingAsync(block);
        await mainWindow!.RefreshOpenRoomBoardAsync();
    }

    private Task CreateTodoFromDesktopAsync(Window owner)
    {
        return mainWindow?.OpenQuickAddAsync(owner, QuickAddKind.Todo) ?? Task.CompletedTask;
    }

    private Task DeleteScheduleFromDesktopAsync(Guid eventId)
    {
        return mainWindow?.DeleteScheduleAsync(eventId) ?? Task.CompletedTask;
    }

    private Task CompleteTodoFromDesktopAsync(Guid todoId)
    {
        return mainWindow?.CompleteTodoAsync(todoId) ?? Task.CompletedTask;
    }

    private async Task ReloadDesktopWindowsAsync()
    {
        await Task.WhenAll(
            desktopWorkbench?.ReloadAsync() ?? Task.CompletedTask,
            desktopAgenda?.ReloadAsync() ?? Task.CompletedTask,
            desktopTodo?.ReloadAsync() ?? Task.CompletedTask);

        await SyncAllLocalRoomEventsAsync();
        if (mainWindow is not null)
        {
            await mainWindow.RefreshOpenRoomBoardAsync();
        }
    }

    private async Task SyncAllLocalRoomEventsAsync()
    {
        if (teamRoomBoardService is null || mainWindow is null)
        {
            return;
        }

        TimeZoneInfo zone = TimeZoneInfo.Local;
        foreach (DateOnly date in mainWindow.GetTimedEventDates())
        {
            TeamRoomBoardData? data = await teamRoomBoardService.LoadAsync(
                date,
                zone,
                CancellationToken.None);
            if (data is not null)
            {
                await SyncLocalRoomEventsAsync(data, date, zone);
            }
        }
    }

    private void SaveAppSettings()
    {
        SaveAppSettingsAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task SaveAppSettingsAsync(CancellationToken cancellationToken)
    {
        if (settingsStore is null)
        {
            return;
        }

        settings = settings with
        {
            DesktopWorkbenchCalendar = desktopWorkbench?.LayoutSettings
                ?? settings.DesktopWorkbenchCalendar,
            DesktopWorkbenchBehavior = desktopWorkbench?.BehaviorSettings
                ?? settings.DesktopWorkbenchBehavior,
            DesktopAgendaBehavior = desktopAgenda?.BehaviorSettings
                ?? settings.DesktopAgendaBehavior,
            DesktopTodoBehavior = desktopTodo?.BehaviorSettings
                ?? settings.DesktopTodoBehavior,
            DesktopWorkbenchAppearance = desktopWorkbench?.AppearanceSettings
                ?? settings.DesktopWorkbenchAppearance,
            DesktopAgendaAppearance = desktopAgenda?.AppearanceSettings
                ?? settings.DesktopAgendaAppearance,
            DesktopTodoAppearance = desktopTodo?.AppearanceSettings
                ?? settings.DesktopTodoAppearance,
            DesktopAgendaLayout = desktopAgenda?.PanelLayoutSettings
                ?? settings.DesktopAgendaLayout,
            DesktopTodoLayout = desktopTodo?.PanelLayoutSettings
                ?? settings.DesktopTodoLayout,
        };
        await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private void ToggleDesktopComponent(DesktopComponentKind kind)
    {
        if (isExiting)
        {
            return;
        }

        switch (kind)
        {
            case DesktopComponentKind.Calendar:
                desktopWorkbench?.Toggle();
                settings = settings with { ShowDesktopCalendar = desktopWorkbench?.IsVisible == true };
                break;
            case DesktopComponentKind.Agenda:
                desktopAgenda?.Toggle();
                settings = settings with { ShowDesktopAgenda = desktopAgenda?.IsVisible == true };
                break;
            case DesktopComponentKind.Todo:
                desktopTodo?.Toggle();
                settings = settings with { ShowDesktopTodo = desktopTodo?.IsVisible == true };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private bool IsDesktopComponentVisible(DesktopComponentKind kind)
    {
        return kind switch
        {
            DesktopComponentKind.Calendar => desktopWorkbench?.IsVisible == true,
            DesktopComponentKind.Agenda => desktopAgenda?.IsVisible == true,
            DesktopComponentKind.Todo => desktopTodo?.IsVisible == true,
            _ => false,
        };
    }
}
