using CcCalendar.Core.Rooms;

namespace CcCalendar.Core.Tests.Rooms;

public sealed class RoomBookingTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid OrganizerId = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RoomRequiresWorkspaceNameAndTimeZone()
    {
        Room room = Room.Create(WorkspaceId, " 项目组二楼会议室 ", "Asia/Shanghai");

        Assert.NotEqual(Guid.Empty, room.Id);
        Assert.Equal(WorkspaceId, room.WorkspaceId);
        Assert.Equal("项目组二楼会议室", room.Name);
        Assert.Equal("Asia/Shanghai", room.TimeZoneId);
    }

    [Fact]
    public void BookingNormalizesToUtcAndRejectsNonPositiveDuration()
    {
        RoomBooking booking = RoomBooking.Create(
            WorkspaceId,
            Guid.NewGuid(),
            OrganizerId,
            "评审会",
            Start.ToOffset(TimeSpan.FromHours(8)),
            Start.AddHours(1).ToOffset(TimeSpan.FromHours(8)),
            "Asia/Shanghai");

        Assert.Equal(TimeSpan.Zero, booking.StartAtUtc.Offset);
        Assert.Equal(Start, booking.StartAtUtc);
        Assert.Equal(Start.AddHours(1), booking.EndAtUtc);

        Assert.Throws<ArgumentException>(() => RoomBooking.Create(
            WorkspaceId,
            Guid.NewGuid(),
            OrganizerId,
            "无效预约",
            Start,
            Start,
            "UTC"));
    }

    [Fact]
    public void ConflictDetectorUsesHalfOpenIntervalsAndRoomIdentity()
    {
        RoomBooking candidate = Booking(Guid.NewGuid(), Start, Start.AddHours(1));
        RoomBooking overlapping = Booking(candidate.RoomId, Start.AddMinutes(30), Start.AddHours(2));
        RoomBooking adjacent = Booking(candidate.RoomId, Start.AddHours(1), Start.AddHours(2));
        RoomBooking elsewhere = Booking(Guid.NewGuid(), Start.AddMinutes(30), Start.AddHours(2));

        IReadOnlyList<RoomBooking> conflicts = RoomBookingConflictDetector.FindConflicts(
            candidate,
            [overlapping, adjacent, elsewhere]);

        Assert.Single(conflicts);
        Assert.Equal(overlapping.Id, conflicts[0].Id);
    }

    private static RoomBooking Booking(Guid roomId, DateTimeOffset start, DateTimeOffset end)
    {
        return RoomBooking.Create(
            WorkspaceId,
            roomId,
            OrganizerId,
            "会议",
            start,
            end,
            "UTC");
    }
}
