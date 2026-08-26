using CcCalendar.Core.QuickAdd;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class EventEditStateTests
{
    private static readonly TimeZoneInfo Beijing =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    [Fact]
    public void CreateUpdateRequestUsesEditedLocalDateTimes()
    {
        var state = new EventEditState(
            new DateOnly(2026, 8, 19),
            new TimeOnly(18, 10),
            new TimeOnly(19, 10),
            IsAllDay: false,
            AllDayStart: null,
            AllDayEndExclusive: null);
        Guid eventId = Guid.NewGuid();

        ScheduleUpdateRequest request = state.CreateUpdateRequest(
            eventId,
            "每日例会",
            "项目组二楼会议室",
            Beijing);

        Assert.Equal(eventId, request.EventId);
        Assert.Equal("每日例会", request.Title);
        Assert.Equal("项目组二楼会议室", request.Location);
        Assert.False(request.IsAllDay);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 19, 18, 10, 0, TimeSpan.FromHours(8)),
            request.StartAt);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 19, 19, 10, 0, TimeSpan.FromHours(8)),
            request.EndAt);
        Assert.Equal("China Standard Time", request.TimeZoneId);
    }

    [Fact]
    public void CreateUpdateRequestSupportsAllDaySwitch()
    {
        var state = new EventEditState(
            new DateOnly(2026, 8, 19),
            new TimeOnly(0, 0),
            new TimeOnly(0, 0),
            IsAllDay: true,
            AllDayStart: new DateOnly(2026, 8, 20),
            AllDayEndExclusive: new DateOnly(2026, 8, 21));

        ScheduleUpdateRequest request = state.CreateUpdateRequest(
            Guid.NewGuid(),
            "外出",
            null,
            Beijing);

        Assert.True(request.IsAllDay);
        Assert.Equal(new DateOnly(2026, 8, 20), request.AllDayStart);
        Assert.Equal(new DateOnly(2026, 8, 21), request.AllDayEndExclusive);
        Assert.Null(request.StartAt);
    }

    [Fact]
    public void CreateUpdateRequestRejectsEndBeforeStart()
    {
        var state = new EventEditState(
            new DateOnly(2026, 8, 19),
            new TimeOnly(19, 10),
            new TimeOnly(18, 10),
            IsAllDay: false,
            AllDayStart: null,
            AllDayEndExclusive: null);

        Assert.Throws<ArgumentException>(() =>
            state.CreateUpdateRequest(Guid.NewGuid(), "每日例会", null, Beijing));
    }

    [Fact]
    public void CreateUpdateRequestRejectsBlankTitle()
    {
        var state = new EventEditState(
            new DateOnly(2026, 8, 19),
            new TimeOnly(18, 10),
            new TimeOnly(19, 10),
            IsAllDay: false,
            AllDayStart: null,
            AllDayEndExclusive: null);

        Assert.Throws<ArgumentException>(() =>
            state.CreateUpdateRequest(Guid.NewGuid(), "  ", null, Beijing));
    }
}
