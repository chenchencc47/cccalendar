using System.Globalization;
using CcCalendar.Core.Calendars;
using CcCalendar.Core.Schedules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.ViewModels;

public sealed class CalendarViewModel : ObservableObject
{
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    private readonly DateOnly currentDate;
    private readonly IChineseCalendarService? chineseCalendarService;
    private readonly TimeZoneInfo localTimeZone;
    private IReadOnlyList<CalendarEvent> allEvents = [];
    private CalendarViewMode mode = CalendarViewMode.Month;
    private DateOnly displayAnchor;
    private IReadOnlyList<CalendarDayViewModel> visibleDays = [];
    private IReadOnlyList<CalendarEventDetailViewModel> selectedEventDetails = [];
    private IReadOnlyList<RoomTimelineColumn> roomColumns = [];

    public CalendarViewModel(
        TimeProvider timeProvider,
        IChineseCalendarService? chineseCalendarService = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        currentDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        localTimeZone = timeProvider.LocalTimeZone;
        this.chineseCalendarService = chineseCalendarService;
        displayAnchor = currentDate;
        SelectedDate = currentDate;
        MovePreviousCommand = new RelayCommand(MovePrevious);
        MoveNextCommand = new RelayCommand(MoveNext);
        GoToTodayCommand = new RelayCommand(GoToToday);
        RebuildVisibleDays();
    }

    public CalendarViewMode Mode
    {
        get => mode;
        set
        {
            if (SetProperty(ref mode, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
                RebuildVisibleDays();
            }
        }
    }

    public DateOnly DisplayAnchor
    {
        get => displayAnchor;
        private set
        {
            if (SetProperty(ref displayAnchor, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public DateOnly SelectedDate { get; private set; }

    public string SelectedDateText => SelectedDate.ToString("M月d日 dddd", ChineseCulture);

    public IReadOnlyList<CalendarEventDetailViewModel> SelectedEventDetails
    {
        get => selectedEventDetails;
        private set => SetProperty(ref selectedEventDetails, value);
    }

    public IReadOnlyList<RoomTimelineColumn> RoomColumns
    {
        get => roomColumns;
        private set => SetProperty(ref roomColumns, value);
    }

    public DateOnly RoomViewDate => DisplayAnchor;

    public string DisplayTitle => Mode switch
    {
        CalendarViewMode.Week => $"{DisplayAnchor.ToString("yyyy年M月", ChineseCulture)} · 第{ISOWeek.GetWeekOfYear(DisplayAnchor.ToDateTime(TimeOnly.MinValue))}周",
        CalendarViewMode.Room => $"{DisplayAnchor.ToString("yyyy年M月d日", ChineseCulture)} · 会议室",
        _ => DisplayAnchor.ToString("yyyy年M月", ChineseCulture),
    };

    public IReadOnlyList<CalendarDayViewModel> VisibleDays
    {
        get => visibleDays;
        private set => SetProperty(ref visibleDays, value);
    }

    public IReadOnlyList<CalendarEvent> AllEvents => allEvents;

    public IReadOnlyList<string> HourLabels { get; } =
        [.. Enumerable.Range(RoomTimelineLayout.StartHour, RoomTimelineLayout.EndHour - RoomTimelineLayout.StartHour)
            .Select(hour => $"{hour:00}:00")];

    public IReadOnlyList<object> RowStripes { get; } =
        [.. Enumerable.Range(0, RoomTimelineLayout.TotalRows).Select(_ => new object())];

    public IRelayCommand MovePreviousCommand { get; }

    public IRelayCommand MoveNextCommand { get; }

    public IRelayCommand GoToTodayCommand { get; }

    public void MovePrevious()
    {
        DisplayAnchor = Mode switch
        {
            CalendarViewMode.Month => new DateOnly(DisplayAnchor.Year, DisplayAnchor.Month, 1).AddMonths(-1),
            CalendarViewMode.Room => DisplayAnchor.AddDays(-1),
            _ => DisplayAnchor.AddDays(-7),
        };
        RebuildVisibleDays();
    }

    public void MoveNext()
    {
        DisplayAnchor = Mode switch
        {
            CalendarViewMode.Month => new DateOnly(DisplayAnchor.Year, DisplayAnchor.Month, 1).AddMonths(1),
            CalendarViewMode.Room => DisplayAnchor.AddDays(1),
            _ => DisplayAnchor.AddDays(7),
        };
        RebuildVisibleDays();
    }

    public void GoToToday()
    {
        DisplayAnchor = currentDate;
        RebuildVisibleDays();
    }

    public void Load(IEnumerable<CalendarEvent> calendarEvents)
    {
        ArgumentNullException.ThrowIfNull(calendarEvents);
        allEvents = [.. calendarEvents];
        RebuildVisibleDays();
    }

    public CalendarEvent? FindEvent(Guid eventId)
    {
        return allEvents.SingleOrDefault(calendarEvent => calendarEvent.Id == eventId);
    }

    public void SelectDate(DateOnly date)
    {
        SelectedDate = date;
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(SelectedDateText));
        DisplayAnchor = date;
        RebuildVisibleDays();
    }

    public IReadOnlyList<CalendarEventDetailViewModel> GetEventDetails(DateOnly date)
    {
        return [.. GetEventsOnDate(date).Select(calendarEvent =>
        {
            if (calendarEvent.IsAllDay)
            {
                return new CalendarEventDetailViewModel(
                    calendarEvent.Id,
                    calendarEvent.Title,
                    "全天",
                    true,
                    calendarEvent.Location);
            }

            DateTimeOffset localStart = TimeZoneInfo.ConvertTime(
                calendarEvent.StartAtUtc!.Value,
                localTimeZone);
            DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(
                calendarEvent.EndAtUtc!.Value,
                localTimeZone);
            string timeText = DateOnly.FromDateTime(localStart.DateTime)
                    == DateOnly.FromDateTime(localEnd.DateTime)
                ? $"{localStart:HH:mm}–{localEnd:HH:mm}"
                : $"{localStart:M月d日 HH:mm}–{localEnd:M月d日 HH:mm}";
            return new CalendarEventDetailViewModel(
                calendarEvent.Id,
                calendarEvent.Title,
                timeText,
                false,
                calendarEvent.Location);
        })];
    }

    private void RebuildVisibleDays()
    {
        DateOnly firstVisibleDate = Mode == CalendarViewMode.Month
            ? FindMonthGridStart(DisplayAnchor)
            : FindMonday(DisplayAnchor);
        int dayCount = Mode == CalendarViewMode.Month ? 42 : 7;
        var days = new CalendarDayViewModel[dayCount];

        for (int index = 0; index < dayCount; index++)
        {
            DateOnly calendarDate = firstVisibleDate.AddDays(index);
            ChineseCalendarDayInfo chineseCalendar = chineseCalendarService?.GetDay(calendarDate)
                ?? ChineseCalendarDayInfo.Empty(calendarDate);
            CalendarEvent[] dateEvents = GetEventsOnDate(calendarDate);
            int visibleEventCount = Mode == CalendarViewMode.Week
                ? dateEvents.Length
                : Math.Min(3, dateEvents.Length);
            days[index] = new CalendarDayViewModel(
                calendarDate,
                calendarDate.Month == DisplayAnchor.Month,
                calendarDate == currentDate,
                calendarDate == SelectedDate,
                chineseCalendar,
                [.. dateEvents.Take(visibleEventCount).Select(ToDayEvent)],
                dateEvents.Length - visibleEventCount);
        }

        VisibleDays = days;
        SelectedEventDetails = GetEventDetails(SelectedDate);
        RebuildRoomColumns();
    }

    private void RebuildRoomColumns()
    {
        CalendarEvent[] dayEvents = GetEventsOnDate(DisplayAnchor);
        RoomTimelineColumn[] columns =
        [.. MeetingRoomCatalog.Rooms.Select(room => new RoomTimelineColumn(
            room,
            EventTimelinePreviewBuilder.Build(dayEvents, Guid.Empty, room, localTimeZone)))];
        IReadOnlyList<string> orderedRooms = RoomBookingOrdering.MoveBookedRoomsToFront(
            MeetingRoomCatalog.Rooms,
            columns
            .Where(column => column.Bars.Count > 0)
            .Select(column => column.Room));
        Dictionary<string, RoomTimelineColumn> columnsByRoom =
            columns.ToDictionary(column => column.Room, StringComparer.Ordinal);
        RoomColumns = [.. orderedRooms.Select(room => columnsByRoom[room])];
    }

    public CalendarEvent[] GetEventsOnDate(DateOnly date)
    {
        return [.. allEvents
            .Where(calendarEvent => IsOnDate(calendarEvent, date))
            .OrderByDescending(calendarEvent => calendarEvent.IsAllDay)
            .ThenBy(calendarEvent => calendarEvent.StartAtUtc)];
    }

    private bool IsOnDate(CalendarEvent calendarEvent, DateOnly date)
    {
        if (calendarEvent.IsAllDay)
        {
            return calendarEvent.AllDayStart <= date
                && calendarEvent.AllDayEndExclusive > date;
        }

        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(
            calendarEvent.StartAtUtc!.Value,
            localTimeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(
            calendarEvent.EndAtUtc!.Value.AddTicks(-1),
            localTimeZone);
        return DateOnly.FromDateTime(localStart.DateTime) <= date
            && DateOnly.FromDateTime(localEnd.DateTime) >= date;
    }

    private CalendarDayEventViewModel ToDayEvent(CalendarEvent calendarEvent)
    {
        string timeText = calendarEvent.IsAllDay
            ? "全天"
            : TimeZoneInfo.ConvertTime(
                calendarEvent.StartAtUtc!.Value,
                localTimeZone).ToString("HH:mm", ChineseCulture);
        return new CalendarDayEventViewModel(calendarEvent.Id, calendarEvent.Title, timeText);
    }

    private static DateOnly FindMonthGridStart(DateOnly anchor)
    {
        return FindMonday(new DateOnly(anchor.Year, anchor.Month, 1));
    }

    private static DateOnly FindMonday(DateOnly calendarDate)
    {
        int daysSinceMonday = ((int)calendarDate.DayOfWeek + 6) % 7;
        return calendarDate.AddDays(-daysSinceMonday);
    }
}
