using System.Globalization;
using CcCalendar.Core.Calendars;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class TodayViewModel : ObservableObject
{
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    private IReadOnlyList<CalendarEventDetailViewModel> todaySchedules = [];
    private IReadOnlyList<TodayTodoItemViewModel> todayTodos = [];

    public TodayViewModel(
        TimeProvider timeProvider,
        IChineseCalendarService? chineseCalendarService = null,
        WeatherViewModel? weather = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        Today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        FormattedDate = Today.ToString("yyyy年M月d日 dddd", ChineseCulture);
        ChineseCalendarDayInfo info = chineseCalendarService?.GetDay(Today)
            ?? ChineseCalendarDayInfo.Empty(Today);
        LunarDateText = string.IsNullOrEmpty(info.LunarText)
            ? string.Empty
            : $"农历{info.LunarText}";
        SpecialDayText = !string.IsNullOrEmpty(info.SolarTerm)
            ? info.SolarTerm
            : info.Festivals.Count > 0
                ? info.Festivals[0]
                : info.HolidayName;
        Weather = weather;
    }

    public DateOnly Today { get; }

    public string FormattedDate { get; }

    public string LunarDateText { get; }

    public string SpecialDayText { get; }

    public WeatherViewModel? Weather { get; }

    public IReadOnlyList<CalendarEventDetailViewModel> TodaySchedules
    {
        get => todaySchedules;
        private set => SetProperty(ref todaySchedules, value);
    }

    public IReadOnlyList<TodayTodoItemViewModel> TodayTodos
    {
        get => todayTodos;
        private set => SetProperty(ref todayTodos, value);
    }

    public void Load(
        IEnumerable<CalendarEvent> events,
        IEnumerable<TodoItem> todos,
        TimeProvider timeProvider,
        TimeZoneInfo localTimeZone)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(localTimeZone);

        DateOnly today = Today;

        TodaySchedules = [.. events
            .Where(calendarEvent => IsOnDate(calendarEvent, today, localTimeZone))
            .OrderByDescending(calendarEvent => calendarEvent.IsAllDay)
            .ThenBy(calendarEvent => calendarEvent.StartAtUtc)
            .Select(calendarEvent => ToScheduleDetail(calendarEvent, localTimeZone))];

        TodayTodos = [.. todos
            .Where(todo => IsDueTodayOrUndated(todo, today, localTimeZone))
            .OrderBy(todo => todo.Status == TodoStatus.Completed)
            .ThenBy(todo => todo.DueAtUtc)
            .Select(todo => ToTodoItem(todo, localTimeZone))];

        OnPropertyChanged(nameof(TodaySchedules));
        OnPropertyChanged(nameof(TodayTodos));
    }

    private static bool IsOnDate(CalendarEvent calendarEvent, DateOnly date, TimeZoneInfo localTimeZone)
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

    private static CalendarEventDetailViewModel ToScheduleDetail(
        CalendarEvent calendarEvent,
        TimeZoneInfo localTimeZone)
    {
        if (calendarEvent.IsAllDay)
        {
            return new CalendarEventDetailViewModel(
                calendarEvent.Id,
                calendarEvent.Title,
                "全天",
                true);
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
            false);
    }

    private static bool IsDueTodayOrUndated(TodoItem todo, DateOnly today, TimeZoneInfo localTimeZone)
    {
        if (!todo.DueAtUtc.HasValue)
        {
            return todo.Status != TodoStatus.Completed;
        }

        DateTimeOffset localDue = TimeZoneInfo.ConvertTime(todo.DueAtUtc.Value, localTimeZone);
        return DateOnly.FromDateTime(localDue.DateTime) == today;
    }

    private static TodayTodoItemViewModel ToTodoItem(TodoItem todo, TimeZoneInfo localTimeZone)
    {
        bool isCompleted = todo.Status == TodoStatus.Completed;
        string dueText = !todo.DueAtUtc.HasValue
            ? "无截止时间"
            : $"{TimeZoneInfo.ConvertTime(todo.DueAtUtc.Value, localTimeZone).ToString("HH:mm", ChineseCulture)} 到期";
        return new TodayTodoItemViewModel(todo.Title, isCompleted, dueText);
    }
}

public sealed record TodayTodoItemViewModel(string Title, bool IsCompleted, string DueText);
