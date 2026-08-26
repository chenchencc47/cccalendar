using CcCalendar.Core.Rooms;
using Npgsql;

namespace CcCalendar.Server;

public sealed class PostgresRoomBookingStore(PostgresDatabase database) : IRoomBookingStore
{
    public RoomBooking? FindById(Guid bookingId)
    {
        const string sql = """
            SELECT id, workspace_id, room_id, organizer_id, title,
                   start_at_utc, end_at_utc, time_zone_id, meeting_invitation_text
            FROM room_bookings
            WHERE id = $1
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(bookingId);
        using NpgsqlDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadBooking(reader) : null;
    }

    public RoomBooking? FindByIdempotencyKey(Guid workspaceId, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        const string sql = """
            SELECT id, workspace_id, room_id, organizer_id, title,
                   start_at_utc, end_at_utc, time_zone_id, meeting_invitation_text
            FROM room_bookings
            WHERE workspace_id = $1 AND idempotency_key = $2
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(idempotencyKey.Trim());
        using NpgsqlDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadBooking(reader) : null;
    }

    public IReadOnlyList<RoomBooking> ListByRange(Guid workspaceId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        const string sql = """
            SELECT id, workspace_id, room_id, organizer_id, title,
                   start_at_utc, end_at_utc, time_zone_id, meeting_invitation_text
            FROM room_bookings
            WHERE workspace_id = $1
              AND start_at_utc < $2
              AND end_at_utc > $3
            ORDER BY start_at_utc
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(toUtc);
        command.Parameters.AddWithValue(fromUtc);
        using NpgsqlDataReader reader = command.ExecuteReader();
        List<RoomBooking> bookings = [];
        while (reader.Read())
        {
            bookings.Add(ReadBooking(reader));
        }

        return bookings;
    }

    public bool TryAdd(
        RoomBooking booking,
        string idempotencyKey,
        out IReadOnlyList<RoomBooking> conflicts)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        const string sql = """
            INSERT INTO room_bookings (
                id, workspace_id, room_id, organizer_id, title,
                start_at_utc, end_at_utc, time_zone_id, meeting_invitation_text, idempotency_key)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(booking.Id);
        command.Parameters.AddWithValue(booking.WorkspaceId);
        command.Parameters.AddWithValue(booking.RoomId);
        command.Parameters.AddWithValue(booking.OrganizerId);
        command.Parameters.AddWithValue(booking.Title);
        command.Parameters.AddWithValue(booking.StartAtUtc);
        command.Parameters.AddWithValue(booking.EndAtUtc);
        command.Parameters.AddWithValue(booking.TimeZoneId);
        command.Parameters.AddWithValue((object?)booking.MeetingInvitationText ?? DBNull.Value);
        command.Parameters.AddWithValue(idempotencyKey.Trim());
        try
        {
            command.ExecuteNonQuery();
            conflicts = [];
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            conflicts = [];
            return false;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            conflicts = FindConflicts(booking);
            return false;
        }
    }

    public bool TryDelete(Guid bookingId)
    {
        const string sql = "DELETE FROM room_bookings WHERE id = $1";
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(bookingId);
        return command.ExecuteNonQuery() > 0;
    }

    public bool TryUpdateInvitation(Guid bookingId, string meetingInvitationText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingInvitationText);
        const string sql = "UPDATE room_bookings SET meeting_invitation_text = $2 WHERE id = $1";
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(bookingId);
        command.Parameters.AddWithValue(meetingInvitationText.Trim());
        return command.ExecuteNonQuery() > 0;
    }

    private List<RoomBooking> FindConflicts(RoomBooking booking)
    {
        const string sql = """
            SELECT id, workspace_id, room_id, organizer_id, title,
                   start_at_utc, end_at_utc, time_zone_id, meeting_invitation_text
            FROM room_bookings
            WHERE workspace_id = $1
              AND room_id = $2
              AND start_at_utc < $4
              AND end_at_utc > $3
            ORDER BY start_at_utc
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(booking.WorkspaceId);
        command.Parameters.AddWithValue(booking.RoomId);
        command.Parameters.AddWithValue(booking.StartAtUtc);
        command.Parameters.AddWithValue(booking.EndAtUtc);
        using NpgsqlDataReader reader = command.ExecuteReader();
        var conflicts = new List<RoomBooking>();
        while (reader.Read())
        {
            conflicts.Add(ReadBooking(reader));
        }

        return conflicts;
    }

    private static RoomBooking ReadBooking(NpgsqlDataReader reader)
    {
        return RoomBooking.Restore(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }
}
