using CcCalendar.Core.Rooms;

namespace CcCalendar.Server;

public sealed record CreateBookingRequest(
    Guid RoomId,
    Guid OrganizerId,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string TimeZoneId,
    string? MeetingInvitationText = null);

public sealed record CreateRoomRequest(string Name, string TimeZoneId);

public sealed record UpdateBookingInvitationRequest(string? MeetingInvitationText);

public sealed record BookingResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid RoomId,
    Guid OrganizerId,
    string Title,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string TimeZoneId,
    string OrganizerName,
    string? MeetingInvitationText = null);

public sealed record BookingConflictResponse(
    string Code,
    IReadOnlyList<Guid> BookingIds);

public sealed record SyncPageResponse(
    long Cursor,
    IReadOnlyList<CcCalendar.Core.Sync.SyncChange> Changes);

internal static class BookingResponseMapper
{
    public static BookingResponse ToResponse(
        CcCalendar.Core.Rooms.RoomBooking booking,
        IWorkspaceMembershipStore memberships)
    {
        string organizerName = memberships.Find(booking.WorkspaceId, booking.OrganizerId)?.DisplayName
            ?? string.Empty;
        return new BookingResponse(
            booking.Id,
            booking.WorkspaceId,
            booking.RoomId,
            booking.OrganizerId,
            booking.Title,
            booking.StartAtUtc,
            booking.EndAtUtc,
            booking.TimeZoneId,
            organizerName,
            booking.MeetingInvitationText);
    }
}

internal static class RoomResponseMapper
{
    public static RoomCatalogEntry ToResponse(CcCalendar.Core.Rooms.Room room)
    {
        return new RoomCatalogEntry(
            room.Id,
            room.WorkspaceId,
            room.Name,
            room.TimeZoneId,
            room.IsActive);
    }
}
