using CcCalendar.Core.Calendars;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class DesktopWorkbenchViewModel : ObservableObject
{
    private readonly TimeProvider timeProvider;
    private IReadOnlyList<CalendarEvent> allEvents = [];
    private IReadOnlyList<TodoItem> allTodos = [];
    private DateOnly selectedDate;

    public DesktopWorkbenchViewModel(
        TimeProvider timeProvider,
        IChineseCalendarService? chineseCalendarService = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        selectedDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        Calendar = new CalendarViewModel(timeProvider, chineseCalendarService);
    }

    public CalendarViewModel Calendar { get; }

    public DateOnly SelectedDate
    {
        get => selectedDate;
        private set => SetProperty(ref selectedDate, value);
    }

    public IReadOnlyList<CalendarEvent> SelectedEvents { get; private set; } = [];

    public IReadOnlyList<DesktopAgendaItemViewModel> SelectedEventRows { get; private set; } = [];

    public IReadOnlyList<TodoItem> SelectedTodos { get; private set; } = [];

    public void Load(IEnumerable<CalendarEvent> calendarEvents, IEnumerable<TodoItem> todos)
    {
        ArgumentNullException.ThrowIfNull(calendarEvents);
        ArgumentNullException.ThrowIfNull(todos);
        allEvents = [.. calendarEvents];
        allTodos = [.. todos];
        Calendar.Load(allEvents);
        RefreshSelectedDate();
    }

    public void SelectDate(DateOnly calendarDate)
    {
        SelectedDate = calendarDate;
        RefreshSelectedDate();
    }

    private void RefreshSelectedDate()
    {
        SelectedEvents = [.. allEvents.Where(IsOnSelectedDate).OrderBy(calendarEvent => calendarEvent.StartAtUtc)];
        SelectedEventRows = [.. SelectedEvents.Select(ToAgendaItem)];
        SelectedTodos = [.. allTodos
            .Where(todo => todo.Status != TodoStatus.Completed && IsDueOnSelectedDate(todo))
            .OrderBy(todo => todo.DueAtUtc)];
        OnPropertyChanged(nameof(SelectedEvents));
        OnPropertyChanged(nameof(SelectedEventRows));
        OnPropertyChanged(nameof(SelectedTodos));
    }

    private DesktopAgendaItemViewModel ToAgendaItem(CalendarEvent calendarEvent)
    {
        if (calendarEvent.IsAllDay)
        {
            return new DesktopAgendaItemViewModel(calendarEvent.Id, calendarEvent.Title, "全天");
        }

        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(
            calendarEvent.StartAtUtc!.Value,
            timeProvider.LocalTimeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(
            calendarEvent.EndAtUtc!.Value,
            timeProvider.LocalTimeZone);
        return new DesktopAgendaItemViewModel(
            calendarEvent.Id,
            calendarEvent.Title,
            $"{localStart:HH:mm}–{localEnd:HH:mm}");
    }

    private bool IsOnSelectedDate(CalendarEvent calendarEvent)
    {
        if (calendarEvent.IsAllDay)
        {
            return calendarEvent.AllDayStart <= SelectedDate
                && calendarEvent.AllDayEndExclusive > SelectedDate;
        }

        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(
            calendarEvent.StartAtUtc!.Value,
            timeProvider.LocalTimeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(
            calendarEvent.EndAtUtc!.Value.AddTicks(-1),
            timeProvider.LocalTimeZone);
        return DateOnly.FromDateTime(localStart.DateTime) <= SelectedDate
            && DateOnly.FromDateTime(localEnd.DateTime) >= SelectedDate;
    }

    private bool IsDueOnSelectedDate(TodoItem todo)
    {
        if (!todo.DueAtUtc.HasValue)
        {
            return true;
        }

        DateTimeOffset localDue = TimeZoneInfo.ConvertTime(todo.DueAtUtc.Value, timeProvider.LocalTimeZone);
        return DateOnly.FromDateTime(localDue.DateTime) == SelectedDate;
    }
}

public sealed record DesktopAgendaItemViewModel(Guid Id, string Title, string TimeText);
