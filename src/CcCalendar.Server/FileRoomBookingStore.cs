using System.Text.Json;
using CcCalendar.Core.Rooms;

namespace CcCalendar.Server;

public sealed class FileRoomBookingStore : IRoomBookingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object gate = new();
    private readonly string filePath;
    private readonly List<StoredBooking> bookings;

    public FileRoomBookingStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        filePath = Path.Combine(dataDirectory, "room-bookings.json");
        bookings = Load();
    }

    public RoomBooking? FindByIdempotencyKey(Guid workspaceId, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        lock (gate)
        {
            return bookings
                .Where(item => item.Booking.WorkspaceId == workspaceId)
                .Where(item => string.Equals(item.IdempotencyKey, idempotencyKey.Trim(), StringComparison.Ordinal))
                .Select(item => item.Booking)
                .SingleOrDefault();
        }
    }

    public RoomBooking? FindById(Guid bookingId)
    {
        lock (gate)
        {
            return bookings.FirstOrDefault(item => item.Booking.Id == bookingId)?.Booking;
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
        string normalizedKey = idempotencyKey.Trim();
        lock (gate)
        {
            if (bookings.Any(item => item.Booking.WorkspaceId == booking.WorkspaceId
                && string.Equals(item.IdempotencyKey, normalizedKey, StringComparison.Ordinal)))
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

            bookings.Add(new StoredBooking(booking, normalizedKey));
            Persist();
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
            Persist();
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
            Persist();
            return true;
        }
    }

    private List<StoredBooking> Load()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        BookingSnapshot[] snapshots = JsonSerializer.Deserialize<BookingSnapshot[]>(
            File.ReadAllText(filePath),
            JsonOptions) ?? [];
        return snapshots
            .Select(snapshot => new StoredBooking(
                RoomBooking.Restore(
                    snapshot.Id,
                    snapshot.WorkspaceId,
                    snapshot.RoomId,
                    snapshot.OrganizerId,
                    snapshot.Title,
                    snapshot.StartAtUtc,
                    snapshot.EndAtUtc,
                    snapshot.TimeZoneId,
                    snapshot.MeetingInvitationText),
                snapshot.IdempotencyKey))
            .ToList();
    }

    private void Persist()
    {
        BookingSnapshot[] snapshots = bookings.Select(item => new BookingSnapshot(
            item.Booking.Id,
            item.Booking.WorkspaceId,
            item.Booking.RoomId,
            item.Booking.OrganizerId,
            item.Booking.Title,
            item.Booking.StartAtUtc,
            item.Booking.EndAtUtc,
            item.Booking.TimeZoneId,
            item.Booking.MeetingInvitationText,
            item.IdempotencyKey)).ToArray();
        AtomicJsonFile.Write(filePath, snapshots, JsonOptions);
    }

    private sealed record StoredBooking(RoomBooking Booking, string IdempotencyKey);

    private sealed record BookingSnapshot(
        Guid Id,
        Guid WorkspaceId,
        Guid RoomId,
        Guid OrganizerId,
        string Title,
        DateTimeOffset StartAtUtc,
        DateTimeOffset EndAtUtc,
        string TimeZoneId,
        string? MeetingInvitationText,
        string IdempotencyKey);
}
