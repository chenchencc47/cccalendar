using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop;

public partial class QuickAddWindow : Window, INotifyPropertyChanged
{
    private const int RepeatRangeDefaultDays = 90;

    private readonly TimeProvider timeProvider;
    private QuickAddOption selectedOption = null!;
    private ScheduleTimeOption selectedStartTime = null!;
    private ScheduleTimeOption selectedEndTime = null!;
    private DateTime? selectedDate;
    private string selectedRoom = string.Empty;
    private bool repeatRangeEditedManually;
    private bool suppressRepeatRangeEvents;
    private string? importedMeetingInvitationText;
    private readonly Func<RoomOccupiedBlock, Task>? addRemoteBookingToCalendar;
    private readonly Func<RoomOccupiedBlock, Task>? deleteRemoteBooking;
    private IReadOnlyList<ScheduleTimeOption> timeOptions = [];

    public QuickAddWindow(
        TimeProvider timeProvider,
        DateOnly? eventDate = null,
        Func<DateOnly, IReadOnlyList<CalendarEvent>>? loadDayEvents = null,
        QuickAddKind? initialKind = null,
        Func<DateOnly, TimeZoneInfo, Task<TeamRoomBoardSnapshot?>>? teamBoardLoader = null,
        Func<RoomOccupiedBlock, Task>? addRemoteBookingToCalendar = null,
        Func<RoomOccupiedBlock, Task>? deleteRemoteBooking = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.addRemoteBookingToCalendar = addRemoteBookingToCalendar;
        this.deleteRemoteBooking = deleteRemoteBooking;
        InitializeComponent();
        Options =
        [
            new(QuickAddKind.Todo, "待办"),
            new(QuickAddKind.Event, "日程"),
            new(QuickAddKind.Project, "项目"),
            new(QuickAddKind.Record, "记录"),
        ];
        TimeOptions = ScheduleTimeOption.CreateHalfHourOptions();
        DateTimeOffset localNow = timeProvider.GetLocalNow();
        TimeOnly defaultStart = eventDate.HasValue
            ? new TimeOnly(9, 0)
            : ScheduleTimeOption.RoundUpToHalfHour(
                TimeOnly.FromDateTime(localNow.DateTime));
        TimeOnly defaultEnd = defaultStart.AddHours(1);
        SelectedStartTime = TimeOptions.First(option => option.Value == defaultStart);
        SelectedEndTime = TimeOptions.First(option => option.Value == defaultEnd);
        SelectedDate = (eventDate
            ?? DateOnly.FromDateTime(localNow.DateTime)).ToDateTime(TimeOnly.MinValue);
        // 默认类型为「日程」（可由调用方覆盖，例如桌面待办右键新增预选「待办」）。
        SelectedOption = Options.Single(option => option.Kind == (initialKind ?? QuickAddKind.Event));
        SelectedRoom = MeetingRoomCatalog.SelectionOptions[0];
        MeetingReminderMinutesBox.Text = "10";
        RoomOptions = MeetingRoomCatalog.SelectionOptions;
        InitializeRepeatRange();
        Picker.RoomsChanged += PickerRoomsChanged;
        Picker.BookingDoubleClicked += PickerBookingDoubleClicked;
        Picker.Initialize(
            DateOnly.FromDateTime(SelectedDate ?? DateTime.Today),
            loadDayEvents ?? (_ => (IReadOnlyList<CalendarEvent>)[]),
            timeProvider.LocalTimeZone,
            teamBoardLoader: teamBoardLoader);
        DataContext = this;
        Loaded += (_, _) =>
        {
            UpdateEventFieldsVisibility();
            UpdateMeetingReminderFields();
            TitleBox.Focus();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal Task RefreshTeamBoardAsync() => Picker.RefreshTeamBoardAsync();

    public IReadOnlyList<QuickAddOption> Options { get; }

    public IReadOnlyList<ScheduleTimeOption> TimeOptions
    {
        get => timeOptions;
        private set
        {
            if (ReferenceEquals(timeOptions, value))
            {
                return;
            }

            timeOptions = value;
            OnPropertyChanged(nameof(TimeOptions));
        }
    }

    private IReadOnlyList<string> roomOptions = MeetingRoomCatalog.SelectionOptions;

    public IReadOnlyList<string> RoomOptions
    {
        get => roomOptions;
        private set
        {
            if (!ReferenceEquals(roomOptions, value))
            {
                roomOptions = value;
                OnPropertyChanged(nameof(RoomOptions));
            }
        }
    }

    public QuickAddOption SelectedOption
    {
        get => selectedOption;
        set
        {
            if (selectedOption != value)
            {
                selectedOption = value;
                OnPropertyChanged(nameof(SelectedOption));
            }
        }
    }

    public ScheduleTimeOption SelectedStartTime
    {
        get => selectedStartTime;
        set
        {
            if (selectedStartTime != value)
            {
                selectedStartTime = value;
                OnPropertyChanged(nameof(SelectedStartTime));
                // 选择开始时间后，结束时间自动跟随为开始时间后一小时，
                // 便于默认的一小时会议；随后仍可手动调整结束时间。
                if (selectedEndTime is not null)
                {
                    TimeOnly endCandidate = value.Value.AddHours(1);
                    if (endCandidate <= value.Value)
                    {
                        // 跨过午夜（如 23:30 之后）：钳制到当天最晚的可选项。
                        endCandidate = new TimeOnly(23, 30);
                    }

                    SelectedEndTime = FindTimeOption(endCandidate) ?? selectedEndTime;
                }
            }
        }
    }

    public ScheduleTimeOption SelectedEndTime
    {
        get => selectedEndTime;
        set
        {
            if (selectedEndTime != value)
            {
                selectedEndTime = value;
                OnPropertyChanged(nameof(SelectedEndTime));
            }
        }
    }

    public DateTime? SelectedDate
    {
        get => selectedDate;
        set
        {
            if (selectedDate != value)
            {
                selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                SyncRepeatRangeToSelectedDate();
            }
        }
    }

    public string SelectedRoom
    {
        get => selectedRoom;
        set
        {
            if (selectedRoom != value)
            {
                selectedRoom = value;
                OnPropertyChanged(nameof(SelectedRoom));
            }
        }
    }

    public IReadOnlyList<QuickAddRequest> Requests { get; private set; } = [];

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if (TryBuildRequests(out string? validationError))
        {
            Close();
            return;
        }

        if (validationError is not null)
        {
            ValidationText.Text = validationError;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PasteMeetingClick(object sender, RoutedEventArgs e)
    {
        string clipboardText = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            ValidationText.Text = "剪贴板中没有文本内容。";
            return;
        }

        // 粘贴只填入原文；用户点击“导入”或按 Enter 后才解析。
        MeetingInvitationBox.Text = clipboardText;
        ValidationText.Text = string.Empty;
    }

    private void MeetingInvitationTextChanged(object sender, TextChangedEventArgs e)
    {
        importedMeetingInvitationText = null;
    }

    private void ImportMeetingClick(object sender, RoutedEventArgs e)
    {
        ApplyInvitationFromInput();
    }

    private void MeetingInvitationKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && !e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
        {
            ApplyInvitationFromInput();
            e.Handled = true;
        }
    }

    private void ApplyInvitationFromInput()
    {
        if (ApplyMeetingInvitation(MeetingInvitationBox.Text))
        {
            ValidationText.Text = string.Empty;
        }
        else
        {
            ValidationText.Text = "输入内容不是有效的腾讯会议邀请，请复制完整邀请信息再试。";
        }
    }

    /// <summary>
    /// 解析腾讯会议邀请文本并填充表单：主题、会议号、日期时间（保留原始分钟）、
    /// 与会地点模糊匹配会议室。识别失败返回 false 且不改动任何字段。
    /// </summary>
    internal bool ApplyMeetingInvitation(string text)
    {
        if (!TencentMeetingInvitationParser.TryParse(text, out TencentMeetingInvitation? invitation)
            || invitation is null)
        {
            return false;
        }

        // 会议邀请一定是日程：切到日程类型并解除全天勾选。
        if (SelectedOption.Kind != QuickAddKind.Event)
        {
            SelectedOption = Options.Single(option => option.Kind == QuickAddKind.Event);
            UpdateEventFieldsVisibility();
        }

        if (AllDayCheckBox.IsChecked == true)
        {
            AllDayCheckBox.IsChecked = false;
        }

        TitleBox.Text = invitation.Title;
        MeetingNumberBox.Text = invitation.MeetingNumber ?? string.Empty;
        SelectedDate = invitation.Date.ToDateTime(TimeOnly.MinValue);
        TimeOnly start = invitation.Start;
        TimeOnly end = invitation.End;
        if (end <= start)
        {
            end = start;
        }

        EnsureTimeOption(start);
        EnsureTimeOption(end);
        SelectedStartTime = FindTimeOption(start) ?? selectedStartTime;
        SelectedEndTime = FindTimeOption(end) ?? selectedEndTime;
        SelectedRoom = TencentMeetingInvitationParser.MatchRoom(invitation.Location, RoomOptions);
        importedMeetingInvitationText = text.Trim();
        return true;
    }

    private ScheduleTimeOption? FindTimeOption(TimeOnly value)
    {
        return TimeOptions.FirstOrDefault(option => option.Value == value);
    }

    private void EnsureTimeOption(TimeOnly value)
    {
        if (TimeOptions.Any(option => option.Value == value))
        {
            return;
        }

        TimeOptions = [.. TimeOptions
            .Append(new ScheduleTimeOption(
                value,
                value.ToString("HH:mm", CultureInfo.InvariantCulture)))
            .OrderBy(option => option.Value)];
    }

    internal bool TryBuildRequests(out string? validationError)
    {
        validationError = null;
        string title = TitleBox.Text.Trim();

        if (title.Length == 0)
        {
            TitleBox.Focus();
            return false;
        }

        // 填写了会议号时附加到标题，便于日程详情中直接看到入会号码。
        string meetingNumber = MeetingNumberBox.Text.Trim();
        if (meetingNumber.Length > 0)
        {
            title = $"{title}（腾讯会议 {meetingNumber}）";
        }

        if (SelectedOption.Kind != QuickAddKind.Event)
        {
            // 待办类型允许直接选择四象限；其他类型（项目/记录）不涉及象限。
            TodoQuadrant? quadrant = SelectedOption.Kind == QuickAddKind.Todo
                ? GetSelectedQuadrant()
                : null;
            Requests = [new QuickAddRequest(SelectedOption.Kind, title, null, null, null, Quadrant: quadrant)];
            return true;
        }

        if (!SelectedDate.HasValue)
        {
            validationError = "请选择日期。";
            return false;
        }

        try
        {
            DateOnly selectedCalendarDate = DateOnly.FromDateTime(SelectedDate.Value);
            string? location = MeetingRoomCatalog.NormalizeSelection(SelectedRoom);
            bool isAllDay = AllDayCheckBox.IsChecked == true;
            TimeOnly startTime = isAllDay
                ? TimeOnly.MinValue
                : ScheduleTimeOption.ParseExactMinute(StartTimeBox.Text);
            TimeOnly endTime = isAllDay
                ? TimeOnly.MinValue
                : ScheduleTimeOption.ParseExactMinute(EndTimeBox.Text);
            IReadOnlyList<DateOnly>? repeatDates = GetRepeatDates(selectedCalendarDate, out validationError);
            if (repeatDates is null)
            {
                return false;
            }

            int? reminderLeadMinutes = GetReminderLeadMinutes(out validationError);
            if (validationError is not null)
            {
                return false;
            }

            Requests = [.. repeatDates
                .Select(date =>
                {
                    var state = new ScheduleEditorState(date, startTime, endTime);
                    QuickAddRequest request = isAllDay
                        ? state.CreateAllDayRequest(title)
                        : state.CreateRequest(title, timeProvider.LocalTimeZone);
                    return request with
                    {
                        Location = location,
                        MeetingInvitationText = importedMeetingInvitationText,
                        ReminderLeadMinutes = reminderLeadMinutes,
                    };
                })];
            return true;
        }
        catch (ArgumentException exception)
        {
            validationError = exception.Message;
            return false;
        }
    }

    /// <summary>读取四象限下拉框当前选择；第 0 项“不指定”返回 null，其余映射到对应象限。</summary>
    private TodoQuadrant? GetSelectedQuadrant()
    {
        return QuadrantBox.SelectedIndex switch
        {
            1 => TodoQuadrant.ImportantUrgent,
            2 => TodoQuadrant.ImportantNotUrgent,
            3 => TodoQuadrant.NotImportantUrgent,
            4 => TodoQuadrant.NotImportantNotUrgent,
            _ => null,
        };
    }

    private int? GetReminderLeadMinutes(out string? validationError)
    {
        validationError = null;
        if (MeetingReminderCheckBox.IsChecked != true || AllDayCheckBox.IsChecked == true)
        {
            return null;
        }

        if (!int.TryParse(
                MeetingReminderMinutesBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int minutes)
            || minutes is < 1 or > 1440)
        {
            validationError = "会议提前提醒分钟数必须是 1 到 1440 之间的整数。";
            return null;
        }

        return minutes;
    }

    /// <summary>根据重复模式下拉框计算要创建日程的日期集合；校验失败时返回 null。</summary>
    private IReadOnlyList<DateOnly>? GetRepeatDates(DateOnly selectedCalendarDate, out string? validationError)
    {
        validationError = null;
        if (RepeatModeBox.SelectedIndex != 1)
        {
            return [selectedCalendarDate];
        }

        List<DayOfWeek> weekdays = GetSelectedWeekdays();
        if (weekdays.Count == 0)
        {
            validationError = "请勾选每周重复的周几（一、二、三…）。";
            return null;
        }

        DateOnly rangeStart = DateOnly.FromDateTime(
            RepeatStartPicker.SelectedDate ?? SelectedDate!.Value);
        DateOnly rangeEnd = DateOnly.FromDateTime(
            RepeatEndPicker.SelectedDate ?? SelectedDate!.Value);
        if (rangeEnd < rangeStart)
        {
            validationError = "重复结束日期不能早于开始日期。";
            return null;
        }

        IReadOnlyList<DateOnly> dates = WeeklyRecurrencePlanner.GetDatesInRange(rangeStart, rangeEnd, weekdays);
        if (dates.Count == 0)
        {
            validationError = "所选日期范围内没有勾选的周几，请调整范围或周几。";
            return null;
        }

        return dates;
    }

    private void InitializeRepeatRange()
    {
        suppressRepeatRangeEvents = true;
        DateOnly date = DateOnly.FromDateTime(SelectedDate ?? DateTime.Today);
        RepeatStartPicker.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
        RepeatEndPicker.SelectedDate = date.AddDays(RepeatRangeDefaultDays).ToDateTime(TimeOnly.MinValue);
        suppressRepeatRangeEvents = false;
    }

    private void SyncRepeatRangeToSelectedDate()
    {
        if (repeatRangeEditedManually || SelectedDate is not { } dateTime)
        {
            return;
        }

        suppressRepeatRangeEvents = true;
        DateOnly date = DateOnly.FromDateTime(dateTime);
        RepeatStartPicker.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
        RepeatEndPicker.SelectedDate = date.AddDays(RepeatRangeDefaultDays).ToDateTime(TimeOnly.MinValue);
        suppressRepeatRangeEvents = false;
    }

    private void RepeatModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RepeatPanel is not null)
        {
            RepeatPanel.Visibility = RepeatModeBox.SelectedIndex == 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void RepeatRangeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!suppressRepeatRangeEvents)
        {
            repeatRangeEditedManually = true;
        }
    }

    private List<DayOfWeek> GetSelectedWeekdays()
    {
        List<(CheckBox Box, DayOfWeek Day)> pairs =
        [
            (RepeatMonday, DayOfWeek.Monday),
            (RepeatTuesday, DayOfWeek.Tuesday),
            (RepeatWednesday, DayOfWeek.Wednesday),
            (RepeatThursday, DayOfWeek.Thursday),
            (RepeatFriday, DayOfWeek.Friday),
            (RepeatSaturday, DayOfWeek.Saturday),
            (RepeatSunday, DayOfWeek.Sunday),
        ];
        return [.. pairs.Where(pair => pair.Box.IsChecked == true).Select(pair => pair.Day)];
    }

    private void KindSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateEventFieldsVisibility();
    }

    private void AllDayCheckChanged(object sender, RoutedEventArgs e)
    {
        if (TimeFields is not null)
        {
            TimeFields.IsEnabled = AllDayCheckBox.IsChecked != true;
        }

        UpdateMeetingReminderFields();
    }

    private void MeetingReminderCheckChanged(object sender, RoutedEventArgs e)
    {
        UpdateMeetingReminderFields();
    }

    private void UpdateMeetingReminderFields()
    {
        if (MeetingReminderCheckBox is null || MeetingReminderMinutesBox is null || AllDayCheckBox is null)
        {
            return;
        }

        bool isTimed = AllDayCheckBox.IsChecked != true;
        MeetingReminderCheckBox.IsEnabled = isTimed;
        MeetingReminderMinutesBox.IsEnabled = isTimed && MeetingReminderCheckBox.IsChecked == true;
    }

    private void UpdateEventFieldsVisibility()
    {
        if (EventFields is not null)
        {
            Visibility visibility = SelectedOption?.Kind == QuickAddKind.Event
                ? Visibility.Visible
                : Visibility.Collapsed;
            EventFields.Visibility = visibility;
            MeetingInvitationFields.Visibility = visibility;
        }

        if (TodoFields is not null)
        {
            TodoFields.Visibility = SelectedOption?.Kind == QuickAddKind.Todo
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void PickerRoomsChanged(object? sender, IReadOnlyList<string> rooms)
    {
        RoomOptions = [string.Empty, .. rooms];
        if (!RoomOptions.Contains(SelectedRoom))
        {
            SelectedRoom = RoomOptions[0];
        }
    }

    private void PickerPicked(object sender, RoomBookingPicked picked)
    {
        if (AllDayCheckBox.IsChecked == true)
        {
            AllDayCheckBox.IsChecked = false;
        }

        SelectedDate = picked.Date.ToDateTime(TimeOnly.MinValue);
        SelectedRoom = picked.Room;
        SelectedStartTime = NearestTimeOption(picked.Start);
        SelectedEndTime = NearestTimeOption(
            picked.End <= picked.Start ? new TimeOnly(23, 30) : picked.End);
    }

    private void PickerDateSwitched(object sender, DateOnly date)
    {
        SelectedDate = date.ToDateTime(TimeOnly.MinValue);
    }

    private void PickerBookingDoubleClicked(object? sender, RoomOccupiedBlock block)
    {
        var window = new MeetingDetailsWindow(block, addRemoteBookingToCalendar, deleteRemoteBooking) { Owner = this };
        window.ShowDialog();
    }

    private ScheduleTimeOption NearestTimeOption(TimeOnly time)
    {
        return TimeOptions.Aggregate((best, next) =>
            Math.Abs(next.Value.Ticks - time.Ticks) < Math.Abs(best.Value.Ticks - time.Ticks) ? next : best);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
