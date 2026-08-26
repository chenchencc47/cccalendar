using CcCalendar.Core.Rooms;
using CcCalendar.Server;

namespace CcCalendar.Server.Tests;

public sealed class FileRoomStoreTests
{
    [Fact]
    public void RoomCatalogSurvivesStoreRecreation()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            Room room = Room.Create(Guid.NewGuid(), "Atlas", "Asia/Shanghai");
            var first = new FileRoomCatalogStore(directory);
            first.Add(room);

            var reopened = new FileRoomCatalogStore(directory);
            Room[] rooms = reopened.List(room.WorkspaceId).ToArray();

            Room persisted = Assert.Single(rooms);
            Assert.Equal(room.Id, persisted.Id);
            Assert.Equal(room.Name, persisted.Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BookingAndIdempotencySurviveStoreRecreation()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            RoomBooking booking = RoomBooking.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Planning",
                new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero),
                "UTC");
            var first = new FileRoomBookingStore(directory);
            Assert.True(first.TryAdd(booking, "idempotency-1", out _));

            var reopened = new FileRoomBookingStore(directory);
            RoomBooking? persisted = reopened.FindByIdempotencyKey(
                booking.WorkspaceId,
                "idempotency-1");

            Assert.NotNull(persisted);
            Assert.Equal(booking.Id, persisted.Id);
            Assert.False(reopened.TryAdd(
                RoomBooking.Create(
                    booking.WorkspaceId,
                    booking.RoomId,
                    booking.OrganizerId,
                    "Overlap",
                    booking.StartAtUtc,
                    booking.EndAtUtc,
                    "UTC"),
                "idempotency-2",
                out IReadOnlyList<RoomBooking> conflicts));
            Assert.Contains(conflicts, item => item.Id == booking.Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cccalendar-server-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        return directory;
    }
}
