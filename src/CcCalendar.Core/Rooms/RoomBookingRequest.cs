namespace CcCalendar.Core.Rooms;

public sealed record RoomBookingRequest(
    Guid RoomId,
    Guid OrganizerId,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string TimeZoneId,
    string? MeetingInvitationText = null);
