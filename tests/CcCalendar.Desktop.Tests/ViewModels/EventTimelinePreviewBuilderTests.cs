using CcCalendar.Core.Schedules;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class EventTimelinePreviewBuilderTests
{
    private static readonly TimeZoneInfo Beijing =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    [Fact]
    public void BuildHighlightsOnlyTheEditingEventAmongRoomBookings()
    {
        CalendarEvent editing = Timed("每日例会", "聚英堂会议室", 9, 0, 1);
        CalendarEvent other = Timed("评审会", "聚英堂会议室", 14, 0, 1);
        CalendarEvent elsewhere = Timed("别会议室", "院士办会议室", 10, 0, 1);

        IReadOnlyList<TimelineBookingBar> bars = EventTimelinePreviewBuilder.Build(
            [editing, other, elsewhere],
            editing.Id,
            "聚英堂会议室",
            Beijing);

        Assert.Equal(2, bars.Count);
        Assert.Contains(bars, bar => bar.IsEditing && bar.Title == "每日例会");
        Assert.Contains(bars, bar => !bar.IsEditing && bar.Title == "评审会");
    }

    [Fact]
    public void BuildWithoutRoomShowsAllTimedEvents()
    {
        CalendarEvent editing = Timed("每日例会", null, 9, 0, 1);
        CalendarEvent other = Timed("评审会", "聚英堂会议室", 14, 0, 1);

        IReadOnlyList<TimelineBookingBar> bars = EventTimelinePreviewBuilder.Build(
            [editing, other],
            editing.Id,
            null,
            Beijing);

        Assert.Equal(2, bars.Count);
        Assert.Equal(52, bars.Single(bar => bar.IsEditing).Geometry.Top);
    }

    [Fact]
    public void BuildSkipsAllDayEvents()
    {
        CalendarEvent allDay = CalendarEvent.CreateAllDay(
            "外出",
            null,
            new DateOnly(2026, 8, 19),
            new DateOnly(2026, 8, 20));

        IReadOnlyList<TimelineBookingBar> bars = EventTimelinePreviewBuilder.Build(
            [allDay],
            allDay.Id,
            null,
            Beijing);

        Assert.Empty(bars);
    }

    private static CalendarEvent Timed(string title, string? location, int hour, int minute, int durationHours)
    {
        return CalendarEvent.CreateTimed(
            title,
            null,
            new DateTimeOffset(2026, 8, 19, hour, minute, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 19, hour + durationHours, minute, 0, TimeSpan.FromHours(8)),
            "China Standard Time",
            location);
    }
}
