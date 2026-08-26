namespace CcCalendar.Core.Rooms;

public interface IRoomBookingClient
{
    Task<IReadOnlyList<RoomCatalogEntry>> GetRoomsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RoomBookingResult>> GetBookingsAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<RoomBookingResult> CreateBookingAsync(
        Guid workspaceId,
        RoomBookingRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task CancelBookingAsync(Guid bookingId, CancellationToken cancellationToken);

    Task UpdateBookingInvitationAsync(
        Guid bookingId,
        string meetingInvitationText,
        CancellationToken cancellationToken);
}
