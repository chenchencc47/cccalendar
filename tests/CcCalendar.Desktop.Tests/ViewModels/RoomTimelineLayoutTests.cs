using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class RoomTimelineLayoutTests
{
    private static readonly TimeZoneInfo Beijing =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    [Fact]
    public void TimelineSpansFourteenHoursWithHalfHourRows()
    {
        Assert.Equal(8, RoomTimelineLayout.StartHour);
        Assert.Equal(22, RoomTimelineLayout.EndHour);
        Assert.Equal(28, RoomTimelineLayout.TotalRows);
        Assert.Equal(728, RoomTimelineLayout.TotalHeight);
    }

    [Fact]
    public void BookingAtNineForOneHourStartsAtSecondRow()
    {
        DateTimeOffset start = ToBeijing(2026, 8, 19, 9, 0);

        RoomBookingGeometry geometry = RoomTimelineLayout.GetGeometry(
            start,
            TimeSpan.FromHours(1),
            Beijing);

        Assert.Equal(52, geometry.Top);
        Assert.Equal(52, geometry.Height);
    }

    [Fact]
    public void BookingBeforeWindowIsClampedToWindowStart()
    {
        DateTimeOffset start = ToBeijing(2026, 8, 19, 7, 0);

        RoomBookingGeometry geometry = RoomTimelineLayout.GetGeometry(
            start,
            TimeSpan.FromHours(2),
            Beijing);

        Assert.Equal(0, geometry.Top);
        Assert.Equal(52, geometry.Height);
    }

    [Fact]
    public void BookingBeyondWindowEndIsClampedToWindowEnd()
    {
        DateTimeOffset start = ToBeijing(2026, 8, 19, 21, 0);

        RoomBookingGeometry geometry = RoomTimelineLayout.GetGeometry(
            start,
            TimeSpan.FromHours(3),
            Beijing);

        Assert.Equal(676, geometry.Top);
        Assert.Equal(52, geometry.Height);
    }

    [Fact]
    public void BookingEntirelyOutsideWindowCollapses()
    {
        RoomBookingGeometry geometry = RoomTimelineLayout.GetGeometry(
            ToBeijing(2026, 8, 19, 23, 0),
            TimeSpan.FromHours(1),
            Beijing);

        Assert.Equal(728, geometry.Top);
        Assert.Equal(0, geometry.Height);
    }

    private static DateTimeOffset ToBeijing(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(8));
    }
}
