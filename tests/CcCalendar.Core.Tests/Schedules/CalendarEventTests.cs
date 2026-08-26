using CcCalendar.Core.Schedules;

namespace CcCalendar.Core.Tests.Schedules;

public sealed class CalendarEventTests
{
    [Fact]
    public void CreateTimedNormalizesInstantsAndKeepsTimeZone()
    {
        var start = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.FromHours(8));
        var end = start.AddHours(1);

        CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
            "Project meeting",
            null,
            start,
            end,
            "China Standard Time");

        Assert.False(calendarEvent.IsAllDay);
        Assert.Equal(start.ToUniversalTime(), calendarEvent.StartAtUtc);
        Assert.Equal(end.ToUniversalTime(), calendarEvent.EndAtUtc);
        Assert.Equal("China Standard Time", calendarEvent.TimeZoneId);
    }

    [Fact]
    public void CreateTimedAllowsCrossDayEvent()
    {
        var start = new DateTimeOffset(2026, 8, 16, 23, 0, 0, TimeSpan.FromHours(8));
        var end = start.AddHours(3);

        CalendarEvent calendarEvent = CalendarEvent.CreateTimed("Release", null, start, end, "China Standard Time");

        Assert.Equal(TimeSpan.FromHours(3), calendarEvent.Duration);
    }

    [Fact]
    public void CreateAllDayUsesExclusiveEndDate()
    {
        CalendarEvent calendarEvent = CalendarEvent.CreateAllDay(
            "Planning",
            null,
            new DateOnly(2026, 8, 16),
            new DateOnly(2026, 8, 18));

        Assert.True(calendarEvent.IsAllDay);
        Assert.Equal(new DateOnly(2026, 8, 16), calendarEvent.AllDayStart);
        Assert.Equal(new DateOnly(2026, 8, 18), calendarEvent.AllDayEndExclusive);
        Assert.Equal(2, calendarEvent.AllDayLength);
    }

    [Fact]
    public void EndMustBeAfterStart()
    {
        var instant = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.CreateTimed("Invalid", null, instant, instant, "UTC"));
        Assert.Throws<ArgumentException>(() =>
            CalendarEvent.CreateAllDay("Invalid", null, new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 16)));
    }

    [Fact]
    public void CreateTimedKeepsOptionalLocation()
    {
        var start = new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.FromHours(8));

        CalendarEvent withRoom = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            start,
            start.AddHours(1),
            "China Standard Time",
            "项目组二楼会议室");
        CalendarEvent withoutRoom = CalendarEvent.CreateTimed(
            "Focus time",
            null,
            start,
            start.AddHours(1),
            "China Standard Time");

        Assert.Equal("项目组二楼会议室", withRoom.Location);
        Assert.Null(withoutRoom.Location);
    }

    [Fact]
    public void UpdateDetailsChangesTitleAndLocation()
    {
        CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero),
            "UTC",
            "聚英堂会议室");

        calendarEvent.UpdateDetails("每周例会", "院士办会议室");

        Assert.Equal("每周例会", calendarEvent.Title);
        Assert.Equal("院士办会议室", calendarEvent.Location);

        calendarEvent.UpdateDetails("每周例会（无会议室）", null);

        Assert.Null(calendarEvent.Location);
    }

    [Fact]
    public void UpdateDetailsRejectsBlankTitle()
    {
        CalendarEvent calendarEvent = CalendarEvent.CreateAllDay(
            "Planning",
            null,
            new DateOnly(2026, 8, 16),
            new DateOnly(2026, 8, 17));

        Assert.Throws<ArgumentException>(() => calendarEvent.UpdateDetails("   ", null));
    }

    [Fact]
    public void MeetingInvitationIsRetainedThroughSevenDaysAndExpiresOnDayEight()
    {
        CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
            "会议",
            null,
            new DateTimeOffset(2026, 8, 27, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 27, 7, 0, 0, TimeSpan.Zero),
            "China Standard Time",
            "会议室",
            "会议主题：会议\n会议时间：2026/08/27 14:00-15:00");

        Assert.Equal("会议主题：会议\n会议时间：2026/08/27 14:00-15:00", calendarEvent.MeetingInvitationText);
        calendarEvent.ClearExpiredMeetingInvitation(new DateOnly(2026, 9, 3));
        Assert.NotNull(calendarEvent.MeetingInvitationText);
        calendarEvent.ClearExpiredMeetingInvitation(new DateOnly(2026, 9, 4));
        Assert.Null(calendarEvent.MeetingInvitationText);
    }

    [Fact]
    public void MeetingReminderLeadMinutesCanBeEnabledOrDisabled()
    {
        var start = new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.FromHours(8));
        CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
            "会议",
            null,
            start,
            start.AddHours(1),
            "China Standard Time",
            reminderLeadMinutes: 30);

        Assert.Equal(30, calendarEvent.ReminderLeadMinutes);

        calendarEvent.UpdateReminderLeadMinutes(null);

        Assert.Null(calendarEvent.ReminderLeadMinutes);
        Assert.Throws<ArgumentOutOfRangeException>(() => calendarEvent.UpdateReminderLeadMinutes(0));
    }

    [Fact]
    public void RescheduleTimedUpdatesInstantsAndCanChangeTimeZone()
    {
        CalendarEvent calendarEvent = CalendarEvent.CreateAllDay(
            "每日例会",
            null,
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 8, 18));

        var start = new DateTimeOffset(2026, 8, 18, 18, 10, 0, TimeSpan.FromHours(8));
        calendarEvent.RescheduleTimed(start, start.AddHours(1), "China Standard Time");

        Assert.False(calendarEvent.IsAllDay);
        Assert.Null(calendarEvent.AllDayStart);
        Assert.Null(calendarEvent.AllDayEndExclusive);
        Assert.Equal(start.ToUniversalTime(), calendarEvent.StartAtUtc);
        Assert.Equal(start.AddHours(1).ToUniversalTime(), calendarEvent.EndAtUtc);
        Assert.Equal("China Standard Time", calendarEvent.TimeZoneId);
    }

    [Fact]
    public void RescheduleTimedRejectsEndBeforeStart()
    {
        CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            new DateTimeOffset(2026, 8, 18, 18, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 18, 19, 10, 0, TimeSpan.Zero),
            "UTC");
        var sameInstant = new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            calendarEvent.RescheduleTimed(sameInstant, sameInstant, "UTC"));
    }

    [Fact]
    public void RescheduleAllDayUpdatesRangeAndClearsTimedFields()
    {
        CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            new DateTimeOffset(2026, 8, 18, 18, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 18, 19, 10, 0, TimeSpan.Zero),
            "UTC");

        calendarEvent.RescheduleAllDay(
            new DateOnly(2026, 8, 19),
            new DateOnly(2026, 8, 21));

        Assert.True(calendarEvent.IsAllDay);
        Assert.Null(calendarEvent.StartAtUtc);
        Assert.Null(calendarEvent.EndAtUtc);
        Assert.Null(calendarEvent.TimeZoneId);
        Assert.Equal(new DateOnly(2026, 8, 19), calendarEvent.AllDayStart);
        Assert.Equal(new DateOnly(2026, 8, 21), calendarEvent.AllDayEndExclusive);
    }
}
