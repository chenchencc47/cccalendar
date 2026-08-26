using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class DesktopWorkbenchViewModelTests
{
    [Fact]
    public void LoadSelectsEventsAndTodosForCurrentDate()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        CalendarEvent todayEvent = CalendarEvent.CreateTimed("Today", null, now, now.AddHours(1), "UTC");
        CalendarEvent tomorrowEvent = CalendarEvent.CreateTimed("Tomorrow", null, now.AddDays(1), now.AddDays(1).AddHours(1), "UTC");
        TodoItem todayTodo = TodoItem.Create("Due today", null, now.AddHours(4));
        TodoItem tomorrowTodo = TodoItem.Create("Due tomorrow", null, now.AddDays(1));
        var viewModel = new DesktopWorkbenchViewModel(new FixedTimeProvider(now));

        viewModel.Load([todayEvent, tomorrowEvent], [todayTodo, tomorrowTodo]);

        Assert.Single(viewModel.SelectedEvents, todayEvent);
        Assert.Single(viewModel.SelectedTodos, todayTodo);
    }

    [Fact]
    public void LoadBuildsAgendaRowsWithLocalTimeRanges()
    {
        var now = new DateTimeOffset(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);
        CalendarEvent timed = CalendarEvent.CreateTimed(
            "A title long enough to wrap in a narrow agenda window",
            null,
            now.AddHours(1),
            now.AddHours(2.5),
            "UTC");
        CalendarEvent allDay = CalendarEvent.CreateAllDay(
            "All day",
            null,
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 8, 18));
        var viewModel = new DesktopWorkbenchViewModel(new FixedTimeProvider(now));

        viewModel.Load([timed, allDay], []);

        Assert.Contains(
            viewModel.SelectedEventRows,
            row => row.Title == timed.Title && row.TimeText == "09:00–10:30");
        Assert.Contains(
            viewModel.SelectedEventRows,
            row => row.Title == allDay.Title && row.TimeText == "全天");
    }

    [Fact]
    public void LoadShowsUndatedActiveTodosAndHidesCompletedTodos()
    {
        var now = new DateTimeOffset(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);
        TodoItem undated = TodoItem.Create("Inbox", null, null);
        TodoItem completed = TodoItem.Create("Done", null, null);
        completed.Complete(now);
        var viewModel = new DesktopWorkbenchViewModel(new FixedTimeProvider(now));

        viewModel.Load([], [undated, completed]);

        Assert.Single(viewModel.SelectedTodos, undated);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
