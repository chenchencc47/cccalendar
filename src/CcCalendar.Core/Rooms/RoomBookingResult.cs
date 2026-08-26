namespace CcCalendar.Core.Rooms;

public sealed record RoomBookingResult(
    Guid Id,
    Guid WorkspaceId,
    Guid RoomId,
    Guid OrganizerId,
    string Title,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string TimeZoneId,
    string OrganizerName = "",
    string? MeetingInvitationText = null);
