using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Desktop.ViewModels;

public sealed record TeamRoomBooking(
    string Room,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string OrganizerName = "",
    Guid Id = default,
    Guid OrganizerId = default,
    string? MeetingInvitationText = null,
    bool IsOwnedByCurrentUser = false);

public sealed record TeamRoomBoardSnapshot(
    IReadOnlyList<string> Rooms,
    IReadOnlyList<TeamRoomBooking> Bookings);

/// <summary>把团队看板数据（房间目录 + RoomId 预约）映射为看板快照（房间名 + 预约）。</summary>
public static class TeamRoomBoardMapper
{
    public static TeamRoomBoardSnapshot ToSnapshot(
        TeamRoomBoardData data,
        Guid? currentUserId = null,
        string? currentUserName = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        Dictionary<Guid, string> activeRooms = data.Rooms
            .Where(room => room.IsActive)
            .ToDictionary(room => room.Id, room => room.Name);
        List<TeamRoomBooking> bookings = [];
        foreach (Core.Rooms.RoomBookingResult booking in data.Bookings)
        {
            if (activeRooms.TryGetValue(booking.RoomId, out string? roomName))
            {
                string organizerName = booking.OrganizerName;
                if (string.IsNullOrWhiteSpace(organizerName)
                    && currentUserId == booking.OrganizerId
                    && !string.IsNullOrWhiteSpace(currentUserName))
                {
                    organizerName = currentUserName.Trim();
                }

                bookings.Add(new TeamRoomBooking(
                    roomName,
                    booking.Title,
                    booking.StartAtUtc,
                    booking.EndAtUtc,
                    organizerName,
                    booking.Id,
                    booking.OrganizerId,
                    booking.MeetingInvitationText,
                    currentUserId == booking.OrganizerId));
            }
        }

        return new TeamRoomBoardSnapshot([.. activeRooms.Values], bookings);
    }
}
