namespace CcCalendar.Desktop.ViewModels;

/// <summary>按当前目录顺序稳定地把当天有预约的会议室移到前面。</summary>
public static class RoomBookingOrdering
{
    public static IReadOnlyList<string> MoveBookedRoomsToFront(
        IReadOnlyList<string> rooms,
        IEnumerable<string> bookedRooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(bookedRooms);

        HashSet<string> booked = bookedRooms.ToHashSet(StringComparer.Ordinal);
        return
        [
            .. rooms.Where(booked.Contains),
            .. rooms.Where(room => !booked.Contains(room)),
        ];
    }
}
