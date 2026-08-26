using CcCalendar.Core.Calendars;
using CcCalendar.Core.Schedules;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class CalendarViewModelTests
{
    [Fact]
    public void MonthViewAlwaysBuildsSixMondayFirstWeeks()
    {
        var viewModel = new CalendarViewModel(new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)));

        Assert.Equal(CalendarViewMode.Month, viewModel.Mode);
        Assert.Equal(42, viewModel.VisibleDays.Count);
        Assert.Equal(new DateOnly(2026, 7, 27), viewModel.VisibleDays[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 6), viewModel.VisibleDays[^1].Date);
        Assert.True(viewModel.VisibleDays.Single(day => day.Date == new DateOnly(2026, 8, 16)).IsToday);
    }

    [Fact]
    public void WeekViewUsesMondayThroughSundayContainingSelectedDate()
    {
        var viewModel = new CalendarViewModel(new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)));

        viewModel.Mode = CalendarViewMode.Week;

        Assert.Equal(7, viewModel.VisibleDays.Count);
        Assert.Equal(new DateOnly(2026, 8, 10), viewModel.VisibleDays[0].Date);
        Assert.Equal(new DateOnly(2026, 8, 16), viewModel.VisibleDays[^1].Date);
    }

    [Fact]
    public void FindEventReturnsLoadedEventByIdOrNull()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        var viewModel = new CalendarViewModel(new FixedTimeProvider(now));
        CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            now.AddHours(1),
            now.AddHours(2),
            "UTC",
            "聚英堂会议室");

        viewModel.Load([calendarEvent]);

        Assert.Same(calendarEvent, viewModel.FindEvent(calendarEvent.Id));
        Assert.Null(viewModel.FindEvent(Guid.NewGuid()));
    }

    [Fact]
    public void MoveNextAdvancesByCurrentViewMode()
    {
        var viewModel = new CalendarViewModel(new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)));

        viewModel.MoveNext();
        Assert.Equal(new DateOnly(2026, 9, 1), viewModel.DisplayAnchor);

        viewModel.Mode = CalendarViewMode.Week;
        viewModel.MoveNext();
        Assert.Equal(new DateOnly(2026, 9, 8), viewModel.DisplayAnchor);
    }

    [Fact]
    public void ChineseCalendarMetadataFlowsIntoVisibleDayAndWeekTitle()
    {
        var viewModel = new CalendarViewModel(
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)),
            new FakeChineseCalendarService());

        CalendarDayViewModel today = viewModel.VisibleDays.Single(day => day.Date == new DateOnly(2026, 8, 16));
        viewModel.Mode = CalendarViewMode.Week;

        Assert.Equal("初四", today.LunarText);
        Assert.Equal("测试节", today.SecondaryText);
        Assert.Equal("休", today.DayBadgeText);
        Assert.Contains("第33周", viewModel.DisplayTitle);
    }

    [Fact]
    public void LoadShowsAtMostThreeOrderedEventsAndOverflowCountPerDay()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        var viewModel = new CalendarViewModel(new FixedTimeProvider(now));
        CalendarEvent[] events =
        [
            CalendarEvent.CreateTimed("11:00", null, now.AddHours(3), now.AddHours(4), "UTC"),
            CalendarEvent.CreateTimed("09:00", null, now.AddHours(1), now.AddHours(2), "UTC"),
            CalendarEvent.CreateAllDay("All day", null, new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 17)),
            CalendarEvent.CreateTimed("10:00", null, now.AddHours(2), now.AddHours(3), "UTC"),
        ];

        viewModel.Load(events);

        CalendarDayViewModel day = viewModel.VisibleDays.Single(item => item.Date == new DateOnly(2026, 8, 16));
        Assert.Equal(["All day", "09:00", "10:00"], day.Events.Select(item => item.Title));
        Assert.Equal(1, day.RemainingEventCount);
    }

    [Fact]
    public void WeekViewShowsEveryEventWithoutOverflowCount()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        var viewModel = new CalendarViewModel(new FixedTimeProvider(now));
        CalendarEvent[] events =
        [
            CalendarEvent.CreateTimed("11:00", null, now.AddHours(3), now.AddHours(4), "UTC"),
            CalendarEvent.CreateTimed("09:00", null, now.AddHours(1), now.AddHours(2), "UTC"),
            CalendarEvent.CreateAllDay("All day", null, new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 17)),
            CalendarEvent.CreateTimed("10:00", null, now.AddHours(2), now.AddHours(3), "UTC"),
        ];
        viewModel.Load(events);

        viewModel.Mode = CalendarViewMode.Week;

        CalendarDayViewModel day = viewModel.VisibleDays.Single(item => item.Date == new DateOnly(2026, 8, 16));
        Assert.Equal(["All day", "09:00", "10:00", "11:00"], day.Events.Select(item => item.Title));
        Assert.Equal(0, day.RemainingEventCount);
    }

    [Fact]
    public void EventDetailsIncludeEveryEventWithFullLocalTimeRange()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        var viewModel = new CalendarViewModel(new FixedTimeProvider(now));
        CalendarEvent[] events =
        [
            CalendarEvent.CreateTimed("Third", null, now.AddHours(5), now.AddHours(6), "UTC"),
            CalendarEvent.CreateTimed("Review", null, now.AddHours(1).AddMinutes(5), now.AddHours(2).AddMinutes(10), "UTC"),
            CalendarEvent.CreateAllDay("Holiday", null, new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 17)),
            CalendarEvent.CreateTimed("Second", null, now.AddHours(3), now.AddHours(4), "UTC"),
        ];
        viewModel.Load(events);

        IReadOnlyList<CalendarEventDetailViewModel> details = viewModel.GetEventDetails(
            new DateOnly(2026, 8, 16));

        Assert.Equal(4, details.Count);
        Assert.Equal("Holiday", details[0].Title);
        Assert.Equal("全天", details[0].TimeText);
        Assert.Equal("Review", details[1].Title);
        Assert.Equal("09:05–10:10", details[1].TimeText);
    }

    [Fact]
    public void SelectingDateRefreshesTheWorkspaceDetails()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        var viewModel = new CalendarViewModel(new FixedTimeProvider(now));
        CalendarEvent review = CalendarEvent.CreateTimed(
            "Review",
            null,
            now.AddDays(1).AddHours(1),
            now.AddDays(1).AddHours(2),
            "UTC");
        viewModel.Load([review]);

        viewModel.SelectDate(new DateOnly(2026, 8, 17));

        Assert.Equal("8月17日 星期一", viewModel.SelectedDateText);
        CalendarEventDetailViewModel details = Assert.Single(viewModel.SelectedEventDetails);
        Assert.Equal("Review", details.Title);
        Assert.Equal("09:00–10:00", details.TimeText);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeChineseCalendarService : IChineseCalendarService
    {
        public ChineseCalendarDayInfo GetDay(DateOnly calendarDate)
        {
            return calendarDate == new DateOnly(2026, 8, 16)
                ? new ChineseCalendarDayInfo(
                    calendarDate,
                    "初四",
                    string.Empty,
                    ["测试节"],
                    ChineseDayType.PublicHoliday,
                    "测试节",
                    calendarDate,
                    33)
                : ChineseCalendarDayInfo.Empty(calendarDate);
        }
    }
}
