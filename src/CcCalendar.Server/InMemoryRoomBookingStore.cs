using CcCalendar.Core.Rooms;

namespace CcCalendar.Server;

public sealed class InMemoryRoomBookingStore : IRoomBookingStore
{
    private readonly object gate = new();
    private readonly List<StoredBooking> bookings = [];

    public RoomBooking? FindById(Guid bookingId)
    {
        lock (gate)
        {
            return bookings.FirstOrDefault(item => item.Booking.Id == bookingId)?.Booking;
        }
    }

    public RoomBooking? FindByIdempotencyKey(Guid workspaceId, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        lock (gate)
        {
            return bookings
                .Where(item => item.Booking.WorkspaceId == workspaceId)
                .Where(item => string.Equals(
                    item.IdempotencyKey,
                    idempotencyKey,
                    StringComparison.Ordinal))
                .Select(item => item.Booking)
                .SingleOrDefault();
        }
    }

    public IReadOnlyList<RoomBooking> ListByRange(Guid workspaceId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        lock (gate)
        {
            return bookings
                .Where(item => item.Booking.WorkspaceId == workspaceId)
                .Where(item => item.Booking.StartAtUtc < toUtc && item.Booking.EndAtUtc > fromUtc)
                .OrderBy(item => item.Booking.StartAtUtc)
                .Select(item => item.Booking)
                .ToArray();
        }
    }

    public bool TryAdd(
        RoomBooking booking,
        string idempotencyKey,
        out IReadOnlyList<RoomBooking> conflicts)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        lock (gate)
        {
            if (bookings.Any(item => item.Booking.WorkspaceId == booking.WorkspaceId
                && string.Equals(item.IdempotencyKey, idempotencyKey.Trim(), StringComparison.Ordinal)))
            {
                conflicts = [];
                return false;
            }

            conflicts = RoomBookingConflictDetector.FindConflicts(
                booking,
                bookings.Select(item => item.Booking));
            if (conflicts.Count > 0)
            {
                return false;
            }

            bookings.Add(new StoredBooking(booking, idempotencyKey.Trim()));
            return true;
        }
    }

    public bool TryDelete(Guid bookingId)
    {
        lock (gate)
        {
            int index = bookings.FindIndex(item => item.Booking.Id == bookingId);
            if (index < 0)
            {
                return false;
            }

            bookings.RemoveAt(index);
            return true;
        }
    }

    public bool TryUpdateInvitation(Guid bookingId, string meetingInvitationText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingInvitationText);
        lock (gate)
        {
            StoredBooking? stored = bookings.FirstOrDefault(item => item.Booking.Id == bookingId);
            if (stored is null)
            {
                return false;
            }

            stored.Booking.UpdateMeetingInvitationText(meetingInvitationText);
            return true;
        }
    }

    private sealed record StoredBooking(RoomBooking Booking, string IdempotencyKey);
}
