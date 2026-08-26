namespace CcCalendar.Core.Rooms;

public static class RoomBookingConflictDetector
{
    public static IReadOnlyList<RoomBooking> FindConflicts(
        RoomBooking candidate,
        IEnumerable<RoomBooking> existingBookings)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existingBookings);

        return existingBookings
            .Where(existing => existing.Id != candidate.Id)
            .Where(existing => existing.WorkspaceId == candidate.WorkspaceId)
            .Where(existing => existing.RoomId == candidate.RoomId)
            .Where(existing => candidate.StartAtUtc < existing.EndAtUtc
                && existing.StartAtUtc < candidate.EndAtUtc)
            .OrderBy(existing => existing.StartAtUtc)
            .ToArray();
    }
}
