using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using CcCalendar.Core.AI;
using CcCalendar.Core.Calendars;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.Tools;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;
using CcCalendar.Infrastructure.Calendars;
using CcCalendar.Infrastructure.Persistence;
using CcCalendar.Infrastructure.Tools;
using CcCalendar.Infrastructure.Updates;
using Microsoft.Win32;

namespace CcCalendar.Desktop;

public partial class MainWindow : Window, IDisposable, INotifyPropertyChanged
{
    private readonly CalendarDataService dataService;
    private readonly Func<Task> externalDataChanged = () => Task.CompletedTask;
    private readonly IcsCalendarFileService icsCalendarFileService;
    private readonly MainWindowViewModel viewModel;
    private readonly ClipboardUpdateListener clipboardUpdateListener;
    private readonly Func<DateOnly, TimeZoneInfo, Task<TeamRoomBoardSnapshot?>>? loadTeamRoomBoard;
    private readonly Func<string, string, DateOnly, TimeOnly, TimeOnly, string?, Task<bool>>? createTeamBooking;
    private readonly Func<RoomOccupiedBlock, Task>? deleteRemoteBooking;
    private ApplicationUpdateManifest? pendingUpdate;
    private QuickAddWindow? quickAddWindow;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<ToolNotificationEventArgs>? ToolNotificationRequested;

    public bool IsUpdateAvailable => pendingUpdate is not null;

    public string UpdateVersion => pendingUpdate?.Version ?? string.Empty;

    internal IReadOnlyList<CcCalendar.Core.Schedules.CalendarEvent> GetEventsOnDate(DateOnly date)
    {
        return viewModel.Calendar.GetEventsOnDate(date);
    }

    internal IReadOnlyList<DateOnly> GetTimedEventDates()
    {
        return [.. viewModel.Calendar.AllEvents
            .Where(calendarEvent => !calendarEvent.IsAllDay
                && calendarEvent.StartAtUtc.HasValue
                && calendarEvent.Location is not null)
            .Select(calendarEvent => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(
                    calendarEvent.StartAtUtc!.Value,
                    TimeZoneInfo.Local).DateTime))
            .Distinct()];
    }

    internal Task RefreshOpenRoomBoardAsync()
    {
        return quickAddWindow is { IsVisible: true } dialog
            ? dialog.RefreshTeamBoardAsync()
            : Task.CompletedTask;
    }

    public MainWindow(
        AppearanceSettingsViewModel? appearanceSettings = null,
        IChineseCalendarService? chineseCalendarService = null,
        WeatherViewModel? weather = null,
        IAiAssistantClient? aiAssistantClient = null,
        AiModelCatalog? aiModelCatalog = null,
        WellnessReminderSettings? wellnessSettings = null,
        Action<WellnessReminderSettings>? wellnessSettingsChanged = null,
        Action<DateTimeOffset>? recordActivity = null,
        Func<Task>? externalDataChanged = null,
        Func<DateOnly, TimeZoneInfo, Task<TeamRoomBoardSnapshot?>>? loadTeamRoomBoard = null,
        Func<string, string, DateOnly, TimeOnly, TimeOnly, string?, Task<bool>>? createTeamBooking = null,
        Func<RoomOccupiedBlock, Task>? deleteRemoteBooking = null)
    {
        InitializeComponent();
        this.externalDataChanged = externalDataChanged ?? (() => Task.CompletedTask);
        this.loadTeamRoomBoard = loadTeamRoomBoard;
        this.createTeamBooking = createTeamBooking;
        this.deleteRemoteBooking = deleteRemoteBooking;
        dataService = new CalendarDataService(ApplicationPaths.DatabasePath);
        icsCalendarFileService = new IcsCalendarFileService(ApplicationPaths.DatabasePath);
        var assistant = new AssistantViewModel(
            new CcCalendar.Infrastructure.AI.AiCreationConfirmationService(
                ApplicationPaths.DatabasePath),
            async _ =>
            {
                await ReloadAsync();
                await this.externalDataChanged();
            },
            new CcCalendar.Infrastructure.AI.AiChangeConfirmationService(
                ApplicationPaths.DatabasePath),
            new CcCalendar.Infrastructure.AI.AiPlanningService(
                ApplicationPaths.DatabasePath),
            new CcCalendar.Infrastructure.AI.AiDraftService(
                ApplicationPaths.DatabasePath),
            TimeProvider.System,
            TimeZoneInfo.Local.Id,
            aiAssistantClient,
            aiModelCatalog,
            new CcCalendar.Desktop.AI.AiChatHistoryStore());
        var clipboardHistory = new ClipboardHistoryService(
            ApplicationPaths.DatabasePath,
            ApplicationPaths.ClipboardStorageDirectory,
            TimeProvider.System);
        var tools = new ToolCenterViewModel(
            TimeProvider.System,
            new SqliteFocusSessionStore(ApplicationPaths.DatabasePath),
            wellnessSettings,
            wellnessSettingsChanged,
            recordActivity,
            clipboardHistoryService: clipboardHistory);
        var clipboardCapture = new ClipboardCaptureCoordinator(
            new WindowsClipboardContentReader(),
            clipboardHistory);
        clipboardCapture.Captured += async (_, _) =>
            await tools.SearchClipboardAsync(CancellationToken.None);
        clipboardUpdateListener = new ClipboardUpdateListener(this, clipboardCapture);
        viewModel = new MainWindowViewModel(
            TimeProvider.System,
            appearanceSettings,
            chineseCalendarService,
            weather,
            assistant,
            tools);
        DataContext = viewModel;
        if (viewModel.Tools is not null)
        {
            viewModel.Tools.NotificationRequested += ToolNotificationForwarded;
        }
        CalendarPage.ScheduleRequested += date => _ = OpenScheduleForDateAsync(date, this);
        CalendarPage.DetailsRequested += ShowScheduleDetails;
        ProjectPage.CreateProjectRequested += (_, _) => _ = OpenQuickAddAsync(this, QuickAddKind.Project);
        ProjectPage.DeleteProjectRequested += (_, _) => _ = DeleteSelectedProjectAsync();
        RecordPage.CreateRecordRequested += (_, _) => _ = OpenQuickAddAsync(this, QuickAddKind.Record);
        RecordPage.SaveRequested += RecordPageSaveRequested;
        TodoPage.TodoCreationRequested += title => _ = CreateTodoAsync(title);
        TodoPage.TodoCompletionToggled += todo => _ = ToggleTodoAsync(todo);
        TodoPage.TodoQuadrantChanged += (todo, quadrant) =>
            _ = MoveTodoToQuadrantAsync(todo, quadrant);
        TodoPage.TodoStatusChanged += (todo, status) =>
            _ = MoveTodoToStatusAsync(todo, status);
        Loaded += MainWindowLoaded;
    }

    private async void MainWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await dataService.InitializeAsync(CancellationToken.None);
            await ReloadAsync();
            await viewModel.Tools!.LoadAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"无法打开本地数据：{exception.Message}",
                "cccalendar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void QuickAddClick(object sender, RoutedEventArgs e)
    {
        _ = OpenQuickAddAsync();
    }

    private void ToolNotificationForwarded(object? sender, ToolNotificationEventArgs e)
    {
        ToolNotificationRequested?.Invoke(this, e);
    }

    private void UpdateClick(object sender, RoutedEventArgs e)
    {
        if (pendingUpdate is not { } update
            || !Uri.TryCreate(update.Url, UriKind.Absolute, out Uri? downloadUri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(downloadUri.AbsoluteUri)
        {
            UseShellExecute = true,
        });
    }

    internal void SetAvailableUpdate(ApplicationUpdateManifest update)
    {
        pendingUpdate = update;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateVersion)));
    }

    private async void ImportIcsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            DefaultExt = ".ics",
            Filter = "ICS 日历 (*.ics)|*.ics|所有文件 (*.*)|*.*",
            Multiselect = false,
            Title = "导入 ICS 日历",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            IcsImportResult result = await icsCalendarFileService.ImportAsync(
                dialog.FileName,
                CancellationToken.None);
            await ReloadAsync();
            await externalDataChanged();
            MessageBox.Show(
                this,
                $"已导入 {result.ImportedEventCount} 项日程。",
                "cccalendar",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"无法导入日历：{exception.Message}",
                "cccalendar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void RecordPageSaveRequested(
        object? sender,
        RecordSaveRequestedEventArgs e)
    {
        if (e.RecordId is not Guid recordId)
        {
            return;
        }

        try
        {
            bool changed = await dataService.SaveRecordRevisionAsync(
                recordId,
                e.Content,
                CancellationToken.None);
            await ReloadAsync();
            await externalDataChanged();
            e.ReportStatus(changed ? "已保存版本" : "内容未变化");
        }
        catch (Exception exception)
        {
            e.ReportStatus("保存失败");
            MessageBox.Show(
                this,
                $"无法保存记录版本：{exception.Message}",
                "cccalendar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task DeleteSelectedProjectAsync()
    {
        if (viewModel.Projects.SelectedProject is not { } project)
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            $"确定删除项目“{project.Name}”吗？项目会移入回收站。",
            "删除项目",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await dataService.DeleteProjectAsync(project.Id, CancellationToken.None);
        await ReloadAsync();
        await externalDataChanged();
    }

    private async void ExportIcsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".ics",
            FileName = $"cccalendar-{DateTime.Today:yyyyMMdd}.ics",
            Filter = "ICS 日历 (*.ics)|*.ics",
            OverwritePrompt = true,
            Title = "导出 ICS 日历",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await icsCalendarFileService.ExportAsync(dialog.FileName, CancellationToken.None);
            MessageBox.Show(
                this,
                "日历已导出。",
                "cccalendar",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"无法导出日历：{exception.Message}",
                "cccalendar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public Task OpenQuickAddAsync(Window? owner = null, QuickAddKind initialKind = QuickAddKind.Event)
    {
        if (quickAddWindow is { IsVisible: true } existing)
        {
            existing.Activate();
            return Task.CompletedTask;
        }

        var dialog = new QuickAddWindow(
            TimeProvider.System,
            loadDayEvents: date => viewModel.Calendar.GetEventsOnDate(date),
            initialKind: initialKind,
            teamBoardLoader: loadTeamRoomBoard,
            addRemoteBookingToCalendar: AddRemoteBookingToCalendarAsync,
            deleteRemoteBooking: deleteRemoteBooking)
        {
            Owner = owner ?? this,
        };

        quickAddWindow = dialog;
        dialog.Closed += QuickAddClosed;
        dialog.Show();
        return Task.CompletedTask;
    }

    public Task OpenScheduleForDateAsync(DateOnly date, Window owner)
    {
        if (quickAddWindow is { IsVisible: true } existing)
        {
            existing.Activate();
            return Task.CompletedTask;
        }

        var dialog = new QuickAddWindow(
            TimeProvider.System,
            date,
            loadDayEvents: viewModel.Calendar.GetEventsOnDate,
            teamBoardLoader: loadTeamRoomBoard,
            addRemoteBookingToCalendar: AddRemoteBookingToCalendarAsync,
            deleteRemoteBooking: deleteRemoteBooking)
        {
            Owner = owner,
        };
        quickAddWindow = dialog;
        dialog.Closed += QuickAddClosed;
        dialog.Show();
        return Task.CompletedTask;
    }

    private void QuickAddClosed(object? sender, EventArgs e)
    {
        if (sender is not QuickAddWindow dialog)
        {
            return;
        }

        dialog.Closed -= QuickAddClosed;
        if (ReferenceEquals(quickAddWindow, dialog))
        {
            quickAddWindow = null;
        }

        _ = SaveQuickAddRequestsAsync(dialog);
    }

    private async Task SaveQuickAddRequestsAsync(QuickAddWindow dialog)
    {
        foreach (QuickAddRequest request in dialog.Requests)
        {
            // Submit before the local save triggers the global refresh. The refresh
            // synchronizes local room events as well; saving first caused that path
            // and this explicit path to POST the same booking twice (201 + 409).
            await TrySubmitTeamBookingAsync(request);
            await SaveRequestAsync(request);
        }
    }

    /// <summary>
    /// 团队连接已配置时，把带会议室的定时日程同步提交为在线预约；
    /// 未配置/房间不在团队目录时静默跳过，服务端冲突（409 等）弹窗提示。
    /// </summary>
    private async Task TrySubmitTeamBookingAsync(QuickAddRequest request)
    {
        if (createTeamBooking is null)
        {
            return;
        }

        TeamRoomBookingSubmission? submission = TeamRoomBookingSubmissionResolver.TryCreate(
            request,
            TimeZoneInfo.Local);
        if (submission is null)
        {
            return;
        }

        try
        {
            bool submitted = await createTeamBooking(
                submission.Room,
                submission.Title,
                submission.Date,
                submission.Start,
                submission.End,
                submission.MeetingInvitationText);
            if (!submitted)
            {
                MessageBox.Show(
                    FormatTeamBookingUnavailable(submission.Room),
                    "cccalendar 团队预约",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            string message = exception is HttpRequestException { StatusCode: HttpStatusCode.Conflict }
                ? await FormatTeamBookingConflictAsync(submission, exception)
                : FormatTeamBookingFailure(exception);
            MessageBox.Show(
                message,
                "cccalendar 团队预约",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task<string> FormatTeamBookingConflictAsync(
        TeamRoomBookingSubmission submission,
        Exception exception)
    {
        if (loadTeamRoomBoard is not null)
        {
            try
            {
                TeamRoomBoardSnapshot? snapshot = await loadTeamRoomBoard(
                    submission.Date,
                    TimeZoneInfo.Local);
                TeamRoomBooking[] conflicts = snapshot?.Bookings
                    .Where(booking => string.Equals(booking.Room, submission.Room, StringComparison.Ordinal))
                    .Where(booking => booking.StartUtc < ToUtc(submission.Date, submission.End)
                        && booking.EndUtc > ToUtc(submission.Date, submission.Start))
                    .ToArray()
                    ?? [];
                if (conflicts.Length > 0)
                {
                    string details = string.Join(
                        "\n",
                        conflicts.Select(booking =>
                            $"- {booking.Room} {booking.StartUtc.ToLocalTime():MM月dd日 HH:mm}–{booking.EndUtc.ToLocalTime():HH:mm}，预约人：{(string.IsNullOrWhiteSpace(booking.OrganizerName) ? "未知成员" : booking.OrganizerName)}"));
                    return "会议室在线预约未提交成功（本地日程已保存）：\n\n"
                        + "检测到以下云端预约与当前时段重叠：\n"
                        + details
                        + "\n\n如果这是你之前创建的预约，请在看板双击蓝色预约后删除；如果是他人预约，请更换会议室或调整时间。";
                }
            }
            catch
            {
                // Preserve the original conflict message when the diagnostic refresh fails.
            }
        }

        return FormatTeamBookingFailure(exception);
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time), TimeZoneInfo.Local);
    }

    private async Task AddRemoteBookingToCalendarAsync(RoomOccupiedBlock block)
    {
        if (block.Date == default)
        {
            throw new InvalidOperationException("无法确定会议日期。");
        }

        DateTimeOffset start = new DateTimeOffset(
            block.Date.ToDateTime(block.Start),
            TimeZoneInfo.Local.GetUtcOffset(block.Date.ToDateTime(block.Start)));
        DateTimeOffset end = new DateTimeOffset(
            block.Date.ToDateTime(block.End),
            TimeZoneInfo.Local.GetUtcOffset(block.Date.ToDateTime(block.End)));
        await SaveRequestAsync(new QuickAddRequest(
            QuickAddKind.Event,
            block.Title,
            start,
            end,
            TimeZoneInfo.Local.Id,
            Location: block.Room,
            MeetingInvitationText: block.MeetingInvitationText));
    }

    internal static string FormatTeamBookingFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is HttpRequestException { StatusCode: HttpStatusCode.Conflict })
        {
            return "会议室在线预约未提交成功（本地日程已保存）：\n\n该会议室在此时间段已被预约，请选择其他会议室或调整时间。";
        }

        return $"会议室在线预约未提交成功（本地日程已保存）：\n\n{exception.Message}";
    }

    internal static string FormatTeamBookingUnavailable(string room)
    {
        return $"会议室在线预约未提交成功（本地日程已保存）：\n\n"
            + $"服务器没有接受“{room}”的预约。请确认已登录团队，并重新登录一次以刷新开发令牌姓名和用户身份。";
    }

    private async void ShowScheduleDetails(DateOnly date)
    {
        var dialog = new DayScheduleWindow(
            date,
            viewModel.Calendar.GetEventDetails(date))
        {
            Owner = this,
        };
        bool? result = dialog.ShowDialog();
        foreach (Guid eventId in dialog.DeletedEventIds)
        {
            await DeleteScheduleAsync(eventId);
        }

        if (dialog.EditedEventId is { } eventIdToEdit)
        {
            await OpenEventEditAsync(eventIdToEdit, this);
        }
        else if (result == true)
        {
            await OpenScheduleForDateAsync(date, this);
        }
    }

    public async Task OpenEventEditAsync(Guid eventId, Window owner)
    {
        Core.Schedules.CalendarEvent? calendarEvent = viewModel.Calendar.FindEvent(eventId);
        if (calendarEvent is null)
        {
            return;
        }

        DateOnly eventDate = calendarEvent.IsAllDay
            ? calendarEvent.AllDayStart ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                calendarEvent.StartAtUtc ?? DateTimeOffset.UtcNow,
                TimeZoneInfo.Local).DateTime)
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                calendarEvent.StartAtUtc!.Value,
                TimeZoneInfo.Local).DateTime);
        var dialog = new EventEditWindow(
            calendarEvent,
            viewModel.Calendar.GetEventsOnDate(eventDate),
            TimeZoneInfo.Local)
        {
            Owner = owner,
        };
        if (dialog.ShowDialog() == true && dialog.Request is not null)
        {
            await dataService.UpdateEventAsync(dialog.Request, CancellationToken.None);
            await ReloadAsync();
            await externalDataChanged();
        }
    }

    public async Task DeleteScheduleAsync(Guid eventId)
    {
        await dataService.DeleteEventAsync(eventId, CancellationToken.None);
        await ReloadAsync();
        await externalDataChanged();
    }

    /// <summary>
    /// 删除云端预约后移除其对应的本地镜像日程。
    /// 云端预约和本地日程使用不同的 ID，因此只能按预约的业务字段匹配。
    /// </summary>
    internal async Task DeleteLocalEventsForRemoteBookingAsync(RoomOccupiedBlock block)
    {
        Guid[] eventIds = FindLocalEventIdsForRemoteBooking(
            viewModel.Calendar.AllEvents,
            block,
            TimeZoneInfo.Local);
        if (eventIds.Length == 0)
        {
            return;
        }

        foreach (Guid eventId in eventIds)
        {
            await dataService.DeleteEventAsync(eventId, CancellationToken.None);
        }

        await ReloadAsync();
        await externalDataChanged();
    }

    internal static Guid[] FindLocalEventIdsForRemoteBooking(
        IEnumerable<CcCalendar.Core.Schedules.CalendarEvent> events,
        RoomOccupiedBlock block,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(zone);

        return events
            .Where(calendarEvent => !calendarEvent.IsAllDay
                && calendarEvent.StartAtUtc.HasValue
                && calendarEvent.EndAtUtc.HasValue
                && string.Equals(calendarEvent.Location, block.Room, StringComparison.Ordinal)
                && string.Equals(calendarEvent.Title, block.Title, StringComparison.Ordinal))
            .Where(calendarEvent =>
            {
                DateTime startLocal = TimeZoneInfo.ConvertTime(
                    calendarEvent.StartAtUtc!.Value,
                    zone).DateTime;
                DateTime endLocal = TimeZoneInfo.ConvertTime(
                    calendarEvent.EndAtUtc!.Value,
                    zone).DateTime;
                DateOnly expectedEndDate = block.End > block.Start
                    ? block.Date
                    : block.Date.AddDays(1);
                return DateOnly.FromDateTime(startLocal) == block.Date
                    && TimeOnly.FromDateTime(startLocal) == block.Start
                    && DateOnly.FromDateTime(endLocal) == expectedEndDate
                    && TimeOnly.FromDateTime(endLocal) == block.End;
            })
            .Select(calendarEvent => calendarEvent.Id)
            .ToArray();
    }

    private async Task CreateTodoAsync(string title)
    {
        await dataService.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Todo, title, null, null, null),
            CancellationToken.None);
        await ReloadAsync();
        await externalDataChanged();
    }

    private async Task ToggleTodoAsync(TodoItem todo)
    {
        if (todo.Status == TodoStatus.Completed)
        {
            await dataService.ReopenTodoAsync(todo.Id, CancellationToken.None);
        }
        else
        {
            await dataService.CompleteTodoAsync(todo.Id, CancellationToken.None);
        }

        await ReloadAsync();
        await externalDataChanged();
    }

    public async Task CompleteTodoAsync(Guid todoId)
    {
        await dataService.CompleteTodoAsync(todoId, CancellationToken.None);
        await ReloadAsync();
        await externalDataChanged();
    }

    private async Task MoveTodoToQuadrantAsync(TodoItem todo, TodoQuadrant quadrant)
    {
        await dataService.MoveTodoToQuadrantAsync(todo.Id, quadrant, CancellationToken.None);
        await ReloadAsync();
        await externalDataChanged();
    }

    private async Task MoveTodoToStatusAsync(TodoItem todo, TodoStatus status)
    {
        await dataService.MoveTodoToStatusAsync(todo.Id, status, CancellationToken.None);
        await ReloadAsync();
        await externalDataChanged();
    }

    private void SearchResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: GlobalSearchResultViewModel result })
        {
            return;
        }

        viewModel.SelectedItem = viewModel.NavigationItems.Single(
            item => item.Destination == result.Destination);
        viewModel.Search.Query = string.Empty;
    }

    private async Task ReloadAsync()
    {
        CalendarDataSnapshot snapshot = await dataService.LoadAsync(CancellationToken.None);
        viewModel.Projects.Load(snapshot.Projects, snapshot.Todos);
        viewModel.Todos.Load(snapshot.Todos);
        viewModel.Records.Load(snapshot.Records);
        viewModel.Calendar.Load(snapshot.CalendarEvents);
        viewModel.Today.Load(
            snapshot.CalendarEvents,
            snapshot.Todos,
            TimeProvider.System,
            TimeZoneInfo.Local);
        viewModel.Search.Load(snapshot.Projects, snapshot.Todos, snapshot.CalendarEvents, snapshot.Records);
        viewModel.Statistics.Load(snapshot.Projects, snapshot.Todos, snapshot.FocusSessions);
    }

    private async Task SaveRequestAsync(CcCalendar.Core.QuickAdd.QuickAddRequest request)
    {
        await dataService.QuickAddAsync(request, CancellationToken.None);
        await ReloadAsync();
        await externalDataChanged();
    }

    public void Dispose()
    {
        clipboardUpdateListener.Dispose();
        GC.SuppressFinalize(this);
    }
}
