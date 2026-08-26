using System.ComponentModel;
using System.Globalization;
using System.Windows;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Core.Schedules;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop;

public partial class EventEditWindow : Window, INotifyPropertyChanged
{
    private readonly CalendarEvent calendarEvent;
    private readonly TimeZoneInfo timeZone;

    private string eventTitle = string.Empty;
    private string selectedRoom = string.Empty;
    private bool isAllDay;
    private DateTime? selectedDate;
    private ScheduleTimeOption selectedStartTime = null!;
    private ScheduleTimeOption selectedEndTime = null!;

    public EventEditWindow(
        CalendarEvent calendarEvent,
        IReadOnlyList<CalendarEvent> dayEvents,
        TimeZoneInfo timeZone,
        Func<DateOnly, IReadOnlyList<CalendarEvent>>? loadDayEvents = null)
    {
        this.calendarEvent = calendarEvent ?? throw new ArgumentNullException(nameof(calendarEvent));
        this.timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
        InitializeComponent();

        EventTitle = calendarEvent.Title;
        IsAllDay = calendarEvent.IsAllDay;
        MeetingReminderMinutesBox.Text = (calendarEvent.ReminderLeadMinutes ?? 10).ToString(CultureInfo.InvariantCulture);
        MeetingReminderCheckBox.IsChecked = calendarEvent.ReminderLeadMinutes.HasValue;
        UpdateMeetingReminderFields();
        RoomOptions = MeetingRoomCatalog.SelectionOptions;
        SelectedRoom = MeetingRoomCatalog.SelectionOptions.Contains(calendarEvent.Location ?? string.Empty)
            ? calendarEvent.Location!
            : string.Empty;

        TimeOptions = ScheduleTimeOption.CreateHalfHourOptions();
        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(
            calendarEvent.StartAtUtc ?? DateTimeOffset.MinValue,
            timeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(
            calendarEvent.EndAtUtc ?? DateTimeOffset.MinValue,
            timeZone);
        if (calendarEvent.IsAllDay)
        {
            selectedDate = (calendarEvent.AllDayStart ?? DateOnly.FromDateTime(localStart.DateTime))
                .ToDateTime(TimeOnly.MinValue);
            SelectedStartTime = TimeOptions.First(option => option.Value == new TimeOnly(9, 0));
            SelectedEndTime = TimeOptions.First(option => option.Value == new TimeOnly(10, 0));
        }
        else
        {
            selectedDate = localStart.DateTime;
            SelectedStartTime = NearestTimeOption(TimeOnly.FromDateTime(localStart.DateTime));
            SelectedEndTime = NearestTimeOption(
                TimeOnly.FromDateTime(localEnd.DateTime) == TimeOnly.MinValue
                    ? new TimeOnly(23, 59)
                    : TimeOnly.FromDateTime(localEnd.DateTime));
        }

        Func<DateOnly, IReadOnlyList<CalendarEvent>> loader = loadDayEvents
            ?? (_ => dayEvents);
        Picker.Initialize(
            DateOnly.FromDateTime(selectedDate.Value),
            loader,
            timeZone,
            calendarEvent.Id);

        DataContext = this;
        Loaded += (_, _) => TitleBox.Focus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> RoomOptions { get; }

    public IReadOnlyList<ScheduleTimeOption> TimeOptions { get; }

    public ScheduleUpdateRequest? Request { get; private set; }

    public string EventTitle
    {
        get => eventTitle;
        set
        {
            if (eventTitle != value)
            {
                eventTitle = value;
                OnPropertyChanged(nameof(EventTitle));
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

    public bool IsAllDay
    {
        get => isAllDay;
        set
        {
            if (isAllDay != value)
            {
                isAllDay = value;
                OnPropertyChanged(nameof(IsAllDay));
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

    private ScheduleTimeOption NearestTimeOption(TimeOnly time)
    {
        return TimeOptions.Aggregate((best, next) =>
            Math.Abs(next.Value.Ticks - time.Ticks) < Math.Abs(best.Value.Ticks - time.Ticks) ? next : best);
    }

    private void AllDayCheckChanged(object sender, RoutedEventArgs e)
    {
        if (TimeFields is not null)
        {
            TimeFields.IsEnabled = IsAllDay != true;
        }

        Picker.IsEnabled = IsAllDay != true;
        UpdateMeetingReminderFields();
    }

    private void MeetingReminderCheckChanged(object sender, RoutedEventArgs e)
    {
        UpdateMeetingReminderFields();
    }

    private void UpdateMeetingReminderFields()
    {
        if (MeetingReminderCheckBox is null || MeetingReminderMinutesBox is null)
        {
            return;
        }

        bool isTimed = IsAllDay != true;
        MeetingReminderCheckBox.IsEnabled = isTimed;
        MeetingReminderMinutesBox.IsEnabled = isTimed && MeetingReminderCheckBox.IsChecked == true;
    }

    private void PickerPicked(object sender, RoomBookingPicked picked)
    {
        IsAllDay = false;
        if (TimeFields is not null)
        {
            TimeFields.IsEnabled = true;
        }

        SelectedDate = picked.Date.ToDateTime(TimeOnly.MinValue);
        SelectedRoom = picked.Room;
        SelectedStartTime = NearestTimeOption(picked.Start);
        SelectedEndTime = NearestTimeOption(
            picked.End <= picked.Start ? new TimeOnly(23, 59) : picked.End);
    }

    private void PickerDateSwitched(object sender, DateOnly date)
    {
        SelectedDate = date.ToDateTime(TimeOnly.MinValue);
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        string title = EventTitle.Trim();
        if (title.Length == 0)
        {
            ValidationText.Text = "请输入标题。";
            TitleBox.Focus();
            return;
        }

        if (SelectedDate is not { } rawDate)
        {
            ValidationText.Text = "请选择日期。";
            return;
        }

        try
        {
            int? reminderLeadMinutes = GetReminderLeadMinutes();
            DateOnly selectedCalendarDate = DateOnly.FromDateTime(rawDate);
            var state = IsAllDay
                ? new EventEditState(
                    selectedCalendarDate,
                    SelectedStartTime.Value,
                    SelectedEndTime.Value,
                    true,
                    selectedCalendarDate,
                    selectedCalendarDate.AddDays(1))
                : new EventEditState(
                    selectedCalendarDate,
                    SelectedStartTime.Value,
                    SelectedEndTime.Value,
                    false,
                    null,
                    null);
            Request = state.CreateUpdateRequest(
                calendarEvent.Id,
                title,
                MeetingRoomCatalog.NormalizeSelection(SelectedRoom),
                timeZone,
                reminderLeadMinutes);
        }
        catch (ArgumentException exception)
        {
            ValidationText.Text = exception.Message;
            return;
        }

        DialogResult = true;
    }

    private int? GetReminderLeadMinutes()
    {
        if (MeetingReminderCheckBox.IsChecked != true || IsAllDay)
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
            throw new ArgumentException("会议提前提醒分钟数必须是 1 到 1440 之间的整数。", nameof(minutes));
        }

        return minutes;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
