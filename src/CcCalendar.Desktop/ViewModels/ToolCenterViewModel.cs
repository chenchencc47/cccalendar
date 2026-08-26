using CcCalendar.Core.Configuration;
using CcCalendar.Core.Tools;
using CcCalendar.Desktop.Tools;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.ViewModels;

public sealed class ToolCenterViewModel : ObservableObject
{
    private CountdownTimer countdown = new(TimeSpan.FromMinutes(10));
    private readonly IClipboardHistoryService? clipboardHistoryService;
    private readonly IFocusSessionStore focusSessionStore;
    private PomodoroTimer pomodoro = new(
        TimeSpan.FromMinutes(25),
        TimeSpan.FromMinutes(5));
    private readonly TimeProvider timeProvider;
    private readonly Action<DateTimeOffset> recordActivity;
    private readonly Action<WellnessReminderSettings> wellnessSettingsChanged;
    private WellnessReminderSettings wellnessSettings;
    private DateTime dateEnd;
    private int dateOffsetDays = 7;
    private DateTime dateStart;
    private int todayFocusMinutes;
    private int todayPomodoroCount;
    private string clipboardSearchQuery = string.Empty;
    private ClipboardHistoryEntry? selectedClipboardEntry;
    private int countdownMinutes = 10;
    private int pomodoroFocusMinutes = 25;
    private int pomodoroBreakMinutes = 5;

    public ToolCenterViewModel(
        TimeProvider timeProvider,
        IFocusSessionStore focusSessionStore,
        WellnessReminderSettings? wellnessSettings = null,
        Action<WellnessReminderSettings>? wellnessSettingsChanged = null,
        Action<DateTimeOffset>? recordActivity = null,
        WindowPinController? windowPin = null,
        IClipboardHistoryService? clipboardHistoryService = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.focusSessionStore = focusSessionStore
            ?? throw new ArgumentNullException(nameof(focusSessionStore));
        this.wellnessSettings = wellnessSettings ?? new WellnessReminderSettings();
        this.wellnessSettingsChanged = wellnessSettingsChanged ?? (_ => { });
        this.recordActivity = recordActivity ?? (_ => { });
        this.clipboardHistoryService = clipboardHistoryService;
        WindowPin = windowPin ?? new WindowPinController(
            new WindowsWindowPinBackend(),
            Environment.ProcessId);
        dateStart = timeProvider.GetLocalNow().Date;
        dateEnd = dateStart.AddDays(30);
        ToggleCountdownCommand = new RelayCommand(StartOrPauseCountdown);
        ResetCountdownCommand = new RelayCommand(ResetCountdown);
        TogglePomodoroCommand = new RelayCommand(StartOrPausePomodoro);
        ResetPomodoroCommand = new RelayCommand(ResetPomodoro);
        RecordActivityCommand = new RelayCommand(() =>
            this.recordActivity(this.timeProvider.GetLocalNow()));
        RefreshTimeUtilities();
    }

    public string CountdownText => Format(countdown.GetRemaining(timeProvider.GetUtcNow()));

    public string CountdownActionText => countdown.State == TimerState.Running ? "暂停" : "开始";

    public string CountdownStatus { get; private set; } = string.Empty;

    public event EventHandler<ToolNotificationEventArgs>? NotificationRequested;

    public int CountdownMinutes
    {
        get => countdownMinutes;
        set
        {
            int normalized = Math.Clamp(value, 1, 1440);
            if (SetProperty(ref countdownMinutes, normalized))
            {
                countdown = new CountdownTimer(TimeSpan.FromMinutes(normalized));
                CountdownStatus = string.Empty;
                RefreshTimerProperties();
            }
        }
    }

    public int PomodoroFocusMinutes
    {
        get => pomodoroFocusMinutes;
        set
        {
            int normalized = Math.Clamp(value, 1, 240);
            if (SetProperty(ref pomodoroFocusMinutes, normalized))
            {
                pomodoro = new PomodoroTimer(
                    TimeSpan.FromMinutes(normalized),
                    TimeSpan.FromMinutes(pomodoroBreakMinutes));
                RefreshTimerProperties();
            }
        }
    }

    public int PomodoroBreakMinutes
    {
        get => pomodoroBreakMinutes;
        set
        {
            int normalized = Math.Clamp(value, 1, 120);
            if (SetProperty(ref pomodoroBreakMinutes, normalized))
            {
                pomodoro = new PomodoroTimer(
                    TimeSpan.FromMinutes(pomodoroFocusMinutes),
                    TimeSpan.FromMinutes(normalized));
                RefreshTimerProperties();
            }
        }
    }

    public string PomodoroText => Format(pomodoro.GetRemaining(timeProvider.GetUtcNow()));

    public string PomodoroActionText => pomodoro.State == TimerState.Running ? "暂停" : "开始";

    public string PomodoroPhaseText => pomodoro.Phase == PomodoroPhase.Focus ? "专注" : "休息";

    public int TodayFocusMinutes
    {
        get => todayFocusMinutes;
        private set => SetProperty(ref todayFocusMinutes, value);
    }

    public int TodayPomodoroCount
    {
        get => todayPomodoroCount;
        private set => SetProperty(ref todayPomodoroCount, value);
    }

    public TimeProgress Progress { get; private set; } = new(0, 0, 0, 0);

    public DateTime DateStart
    {
        get => dateStart;
        set
        {
            if (SetProperty(ref dateStart, value))
            {
                OnPropertyChanged(nameof(DaysBetween));
                OnPropertyChanged(nameof(AddedDate));
            }
        }
    }

    public DateTime DateEnd
    {
        get => dateEnd;
        set
        {
            if (SetProperty(ref dateEnd, value))
            {
                OnPropertyChanged(nameof(DaysBetween));
            }
        }
    }

    public int DateOffsetDays
    {
        get => dateOffsetDays;
        set
        {
            if (SetProperty(ref dateOffsetDays, Math.Clamp(value, -36500, 36500)))
            {
                OnPropertyChanged(nameof(AddedDate));
            }
        }
    }

    public int DaysBetween => DateCalculator.DaysBetween(
        DateOnly.FromDateTime(DateStart),
        DateOnly.FromDateTime(DateEnd));

    public DateOnly AddedDate => DateCalculator.AddDays(
        DateOnly.FromDateTime(DateStart),
        DateOffsetDays);

    public IReadOnlyList<WorldClockViewModel> WorldClocks { get; private set; } = [];

    public WindowPinController WindowPin { get; }

    public IReadOnlyList<ClipboardHistoryEntry> ClipboardEntries { get; private set; } = [];

    public string ClipboardSearchQuery
    {
        get => clipboardSearchQuery;
        set => SetProperty(ref clipboardSearchQuery, value ?? string.Empty);
    }

    public ClipboardHistoryEntry? SelectedClipboardEntry
    {
        get => selectedClipboardEntry;
        set => SetProperty(ref selectedClipboardEntry, value);
    }

    public bool HourlyChimeEnabled
    {
        get => wellnessSettings.HourlyChimeEnabled;
        set => UpdateWellness(wellnessSettings with { HourlyChimeEnabled = value });
    }

    public bool SedentaryReminderEnabled
    {
        get => wellnessSettings.SedentaryReminderEnabled;
        set => UpdateWellness(wellnessSettings with { SedentaryReminderEnabled = value });
    }

    public int SedentaryIntervalMinutes
    {
        get => wellnessSettings.SedentaryIntervalMinutes;
        set => UpdateWellness(wellnessSettings with
        {
            SedentaryIntervalMinutes = Math.Clamp(value, 15, 240),
        });
    }

    public IRelayCommand ToggleCountdownCommand { get; }

    public IRelayCommand ResetCountdownCommand { get; }

    public IRelayCommand TogglePomodoroCommand { get; }

    public IRelayCommand ResetPomodoroCommand { get; }

    public IRelayCommand RecordActivityCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await RefreshStatisticsAsync(cancellationToken);
        await SearchClipboardAsync(cancellationToken);
        WindowPin.Refresh();
        RefreshTimerProperties();
        RefreshTimeUtilities();
    }

    public async Task SearchClipboardAsync(CancellationToken cancellationToken)
    {
        if (clipboardHistoryService is null)
        {
            return;
        }

        Guid? selectedId = SelectedClipboardEntry?.Id;
        ClipboardEntries = await clipboardHistoryService.SearchAsync(
            ClipboardSearchQuery,
            cancellationToken);
        SelectedClipboardEntry = selectedId.HasValue
            ? ClipboardEntries.FirstOrDefault(entry => entry.Id == selectedId.Value)
            : null;
        OnPropertyChanged(nameof(ClipboardEntries));
    }

    public async Task ToggleClipboardFavoriteAsync(CancellationToken cancellationToken)
    {
        if (clipboardHistoryService is null || SelectedClipboardEntry is null)
        {
            return;
        }

        Guid selectedId = SelectedClipboardEntry.Id;
        await clipboardHistoryService.ToggleFavoriteAsync(selectedId, cancellationToken);
        await SearchClipboardAsync(cancellationToken);
        SelectedClipboardEntry = ClipboardEntries.FirstOrDefault(entry => entry.Id == selectedId);
    }

    public async Task<ClipboardPayload?> GetSelectedClipboardPayloadAsync(
        CancellationToken cancellationToken)
    {
        if (clipboardHistoryService is null || SelectedClipboardEntry is null)
        {
            return null;
        }

        return await clipboardHistoryService.GetPayloadAsync(
            SelectedClipboardEntry.Id,
            cancellationToken);
    }

    public void StartOrPauseCountdown()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (countdown.State == TimerState.Running)
        {
            countdown.Pause(now);
        }
        else
        {
            if (countdown.State == TimerState.Completed)
            {
                countdown.Reset();
            }

            countdown.Start(now);
            CountdownStatus = string.Empty;
        }

        RefreshTimerProperties();
        RefreshTimeUtilities();
    }

    public void ResetCountdown()
    {
        countdown.Reset();
        CountdownStatus = string.Empty;
        RefreshTimerProperties();
    }

    public void StartOrPausePomodoro()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (pomodoro.State == TimerState.Running)
        {
            pomodoro.Pause(now);
        }
        else
        {
            pomodoro.Start(now);
        }

        RefreshTimerProperties();
        RefreshTimeUtilities();
    }

    public void ResetPomodoro()
    {
        pomodoro.Reset();
        RefreshTimerProperties();
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (countdown.Tick(now))
        {
            CountdownStatus = "倒计时完成";
            NotificationRequested?.Invoke(
                this,
                new ToolNotificationEventArgs("倒计时完成", "倒计时已结束。"));
        }

        CompletedFocusSession? completed = pomodoro.Tick(now);
        if (completed is not null)
        {
            NotificationRequested?.Invoke(
                this,
                new ToolNotificationEventArgs("专注完成", "本轮专注已完成，进入休息时间。"));
            await focusSessionStore.AddAsync(
                FocusSession.Create(completed.StartedAtUtc, completed.EndedAtUtc),
                cancellationToken);
            await RefreshStatisticsAsync(cancellationToken);
        }

        RefreshTimerProperties();
        RefreshTimeUtilities();
    }

    private async Task RefreshStatisticsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<FocusSession> sessions = await focusSessionStore.LoadAsync(cancellationToken);
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        FocusSession[] todaySessions = [.. sessions.Where(session =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                session.StartedAtUtc,
                timeProvider.LocalTimeZone).DateTime) == today)];
        TodayFocusMinutes = todaySessions.Sum(session => session.DurationMinutes);
        TodayPomodoroCount = todaySessions.Length;
    }

    private void RefreshTimerProperties()
    {
        OnPropertyChanged(nameof(CountdownText));
        OnPropertyChanged(nameof(CountdownActionText));
        OnPropertyChanged(nameof(CountdownStatus));
        OnPropertyChanged(nameof(PomodoroText));
        OnPropertyChanged(nameof(PomodoroActionText));
        OnPropertyChanged(nameof(PomodoroPhaseText));
    }

    private void RefreshTimeUtilities()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(now, timeProvider.LocalTimeZone);
        Progress = TimeProgressCalculator.Calculate(localNow);
        WorldClocks =
        [
            CreateWorldClock("上海", "China Standard Time", now),
            CreateWorldClock("伦敦", "GMT Standard Time", now),
            CreateWorldClock("纽约", "Eastern Standard Time", now),
        ];
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(WorldClocks));
    }

    private static WorldClockViewModel CreateWorldClock(
        string city,
        string timeZoneId,
        DateTimeOffset instant)
    {
        DateTimeOffset local = WorldClock.Convert(
            instant,
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        return new WorldClockViewModel(city, local);
    }

    private void UpdateWellness(WellnessReminderSettings updated)
    {
        if (updated == wellnessSettings)
        {
            return;
        }

        wellnessSettings = updated;
        wellnessSettingsChanged(updated);
        OnPropertyChanged(nameof(HourlyChimeEnabled));
        OnPropertyChanged(nameof(SedentaryReminderEnabled));
        OnPropertyChanged(nameof(SedentaryIntervalMinutes));
    }

    private static string Format(TimeSpan remaining)
    {
        return $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }
}

public sealed record WorldClockViewModel(string City, DateTimeOffset LocalTime);

public sealed class ToolNotificationEventArgs(string title, string message) : EventArgs
{
    public string Title { get; } = title;
    public string Message { get; } = message;
}
