namespace CcCalendar.Core.Rooms;

public sealed class RoomBooking
{
    private RoomBooking()
    {
        Title = null!;
        TimeZoneId = null!;
    }

    private RoomBooking(
        Guid workspaceId,
        Guid roomId,
        Guid organizerId,
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string timeZoneId,
        string? meetingInvitationText)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A booking must belong to a workspace.", nameof(workspaceId));
        }

        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("A booking must reference a room.", nameof(roomId));
        }

        if (organizerId == Guid.Empty)
        {
            throw new ArgumentException("A booking must reference an organizer.", nameof(organizerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        DateTimeOffset normalizedStart = startAt.ToUniversalTime();
        DateTimeOffset normalizedEnd = endAt.ToUniversalTime();
        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("The booking end must be after its start.", nameof(endAt));
        }

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        RoomId = roomId;
        OrganizerId = organizerId;
        Title = title.Trim();
        StartAtUtc = normalizedStart;
        EndAtUtc = normalizedEnd;
        TimeZoneId = timeZoneId.Trim();
        MeetingInvitationText = string.IsNullOrWhiteSpace(meetingInvitationText)
            ? null
            : meetingInvitationText.Trim();
    }

    private RoomBooking(
        Guid id,
        Guid workspaceId,
        Guid roomId,
        Guid organizerId,
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string timeZoneId,
        string? meetingInvitationText)
        : this(workspaceId, roomId, organizerId, title, startAt, endAt, timeZoneId, meetingInvitationText)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A booking must have an identifier.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid RoomId { get; private set; }

    public Guid OrganizerId { get; private set; }

    public string Title { get; private set; }

    public DateTimeOffset StartAtUtc { get; private set; }

    public DateTimeOffset EndAtUtc { get; private set; }

    public string TimeZoneId { get; private set; }

    public string? MeetingInvitationText { get; private set; }

    public void UpdateMeetingInvitationText(string? meetingInvitationText)
    {
        MeetingInvitationText = string.IsNullOrWhiteSpace(meetingInvitationText)
            ? null
            : meetingInvitationText.Trim();
    }

    public static RoomBooking Create(
        Guid workspaceId,
        Guid roomId,
        Guid organizerId,
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string timeZoneId,
        string? meetingInvitationText = null)
    {
        return new RoomBooking(
            workspaceId,
            roomId,
            organizerId,
            title,
            startAt,
            endAt,
            timeZoneId,
            meetingInvitationText);
    }

    public static RoomBooking Restore(
        Guid id,
        Guid workspaceId,
        Guid roomId,
        Guid organizerId,
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string timeZoneId,
        string? meetingInvitationText = null)
    {
        return new RoomBooking(
            id,
            workspaceId,
            roomId,
            organizerId,
            title,
            startAt,
            endAt,
            timeZoneId,
            meetingInvitationText);
    }
}
