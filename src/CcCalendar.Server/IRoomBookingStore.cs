using CcCalendar.Core.Rooms;

namespace CcCalendar.Server;

public interface IRoomBookingStore
{
    RoomBooking? FindById(Guid bookingId);

    RoomBooking? FindByIdempotencyKey(Guid workspaceId, string idempotencyKey);

    IReadOnlyList<RoomBooking> ListByRange(Guid workspaceId, DateTimeOffset fromUtc, DateTimeOffset toUtc);

    bool TryAdd(
        RoomBooking booking,
        string idempotencyKey,
        out IReadOnlyList<RoomBooking> conflicts);

    bool TryDelete(Guid bookingId);

    bool TryUpdateInvitation(Guid bookingId, string meetingInvitationText);
}
