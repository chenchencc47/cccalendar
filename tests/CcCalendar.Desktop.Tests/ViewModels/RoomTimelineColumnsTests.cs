using CcCalendar.Core.Schedules;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class RoomTimelineColumnsTests
{
    [Fact]
    public void RoomViewBuildsColumnPerCatalogRoomWithBookingsForAnchorDate()
    {
        var viewModel = new CalendarViewModel(new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 19, 2, 0, 0, TimeSpan.Zero)));
        CalendarEvent booking = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            new DateTimeOffset(2026, 8, 19, 10, 10, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 19, 11, 10, 0, TimeSpan.FromHours(8)),
            "China Standard Time",
            "聚英堂会议室");

        viewModel.Load([booking]);
        viewModel.Mode = CalendarViewMode.Room;

        Assert.Equal(MeetingRoomCatalog.Rooms.Count, viewModel.RoomColumns.Count);
        Assert.Equal(
            [
                "聚英堂会议室",
                .. MeetingRoomCatalog.Rooms.Where(room => room != "聚英堂会议室"),
            ],
            viewModel.RoomColumns.Select(column => column.Room));
        RoomTimelineColumn juying = viewModel.RoomColumns.Single(column => column.Room == "聚英堂会议室");
        TimelineBookingBar bar = Assert.Single(juying.Bars);
        Assert.Equal("每日例会", bar.Title);
        Assert.True(viewModel.RoomColumns
            .Where(column => column.Room != "聚英堂会议室")
            .All(column => column.Bars.Count == 0));
    }

    [Fact]
    public void RoomViewMovesBySingleDay()
    {
        var viewModel = new CalendarViewModel(new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 19, 2, 0, 0, TimeSpan.Zero)));
        viewModel.Load([]);
        viewModel.Mode = CalendarViewMode.Room;

        Assert.Equal(new DateOnly(2026, 8, 19), viewModel.RoomViewDate);

        viewModel.MoveNext();
        Assert.Equal(new DateOnly(2026, 8, 20), viewModel.RoomViewDate);

        viewModel.MovePrevious();
        viewModel.MovePrevious();
        Assert.Equal(new DateOnly(2026, 8, 18), viewModel.RoomViewDate);
    }

    [Fact]
    public void RoomViewIgnoresAllDayAndOffRoomEvents()
    {
        var viewModel = new CalendarViewModel(new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 19, 2, 0, 0, TimeSpan.Zero)));
        CalendarEvent allDay = CalendarEvent.CreateAllDay(
            "外出",
            null,
            new DateOnly(2026, 8, 19),
            new DateOnly(2026, 8, 20));
        CalendarEvent noRoom = CalendarEvent.CreateTimed(
            "无会议室日程",
            null,
            new DateTimeOffset(2026, 8, 19, 3, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 19, 4, 0, 0, TimeSpan.FromHours(8)),
            "China Standard Time",
            null);

        viewModel.Load([allDay, noRoom]);
        viewModel.Mode = CalendarViewMode.Room;

        Assert.True(viewModel.RoomColumns.All(column => column.Bars.Count == 0));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
