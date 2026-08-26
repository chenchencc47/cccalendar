using CcCalendar.Core.Rooms;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Desktop.Tests.Sync;

public sealed class TeamRoomBoardMapperTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid RoomA = Guid.NewGuid();
    private static readonly Guid RoomB = Guid.NewGuid();

    [Fact]
    public void MapProducesRoomNamesAndNameMappedBookings()
    {
        var data = new TeamRoomBoardData(
            Rooms:
            [
                new RoomCatalogEntry(RoomA, WorkspaceId, "云会议室A", "Asia/Shanghai", true),
                new RoomCatalogEntry(RoomB, WorkspaceId, "云会议室B", "Asia/Shanghai", true),
            ],
            Bookings:
            [
                new RoomBookingResult(
                    Guid.NewGuid(),
                    WorkspaceId,
                    RoomB,
                    Guid.NewGuid(),
                    "远程周会",
                    new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
                    "UTC",
                    "周家丞"),
            ]);

        TeamRoomBoardSnapshot snapshot = TeamRoomBoardMapper.ToSnapshot(data);

        Assert.Equal(["云会议室A", "云会议室B"], snapshot.Rooms);
        TeamRoomBooking booking = Assert.Single(snapshot.Bookings);
        Assert.Equal("云会议室B", booking.Room);
        Assert.Equal("远程周会", booking.Title);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero), booking.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero), booking.EndUtc);
        Assert.Equal("周家丞", booking.OrganizerName);
    }

    [Fact]
    public void MapSkipsBookingsAndInactiveRoomsOutsideTheCatalog()
    {
        var data = new TeamRoomBoardData(
            Rooms:
            [
                new RoomCatalogEntry(RoomA, WorkspaceId, "云会议室A", "Asia/Shanghai", true),
                new RoomCatalogEntry(RoomB, WorkspaceId, "已停用会议室", "Asia/Shanghai", false),
            ],
            Bookings:
            [
                new RoomBookingResult(
                    Guid.NewGuid(),
                    WorkspaceId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "幽灵预约",
                    new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
                    "UTC"),
            ]);

        TeamRoomBoardSnapshot snapshot = TeamRoomBoardMapper.ToSnapshot(data);

        Assert.Equal(["云会议室A"], snapshot.Rooms);
        Assert.Empty(snapshot.Bookings);
    }

    [Fact]
    public void MapFallsBackToCurrentDevTokenNameForOwnLegacyBooking()
    {
        Guid userId = Guid.NewGuid();
        var data = new TeamRoomBoardData(
            [new RoomCatalogEntry(RoomA, WorkspaceId, "云会议室A", "Asia/Shanghai", true)],
            [new RoomBookingResult(
                Guid.NewGuid(),
                WorkspaceId,
                RoomA,
                userId,
                "每日例会",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                "UTC")]);

        TeamRoomBoardSnapshot snapshot = TeamRoomBoardMapper.ToSnapshot(
            data,
            userId,
            "周家丞");

        Assert.Equal("周家丞", Assert.Single(snapshot.Bookings).OrganizerName);
    }

    [Fact]
    public void MapCarriesInvitationAndMarksCurrentUsersBooking()
    {
        Guid userId = Guid.NewGuid();
        string invitation = "会议主题：每日例会\n#腾讯会议：123-456-789";
        var data = new TeamRoomBoardData(
            [new RoomCatalogEntry(RoomA, WorkspaceId, "云会议室A", "Asia/Shanghai", true)],
            [new RoomBookingResult(
                Guid.NewGuid(), WorkspaceId, RoomA, userId, "每日例会",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "UTC", "周家丞", invitation)]);

        TeamRoomBooking booking = Assert.Single(TeamRoomBoardMapper.ToSnapshot(data, userId).Bookings);

        Assert.True(booking.IsOwnedByCurrentUser);
        Assert.Equal(userId, booking.OrganizerId);
        Assert.Equal(invitation, booking.MeetingInvitationText);
    }
}
