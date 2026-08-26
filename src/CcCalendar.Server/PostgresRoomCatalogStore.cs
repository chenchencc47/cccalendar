using CcCalendar.Core.Rooms;
using Npgsql;

namespace CcCalendar.Server;

public sealed class PostgresRoomCatalogStore(PostgresDatabase database) : IRoomCatalogStore
{
    public IReadOnlyList<Room> List(Guid workspaceId)
    {
        const string sql = """
            SELECT id, workspace_id, name, time_zone_id, is_active
            FROM rooms
            WHERE workspace_id = $1
            ORDER BY name, id
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(workspaceId);
        using NpgsqlDataReader reader = command.ExecuteReader();
        var rooms = new List<Room>();
        while (reader.Read())
        {
            rooms.Add(Room.Restore(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4)));
        }

        return rooms;
    }

    public void Add(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        const string sql = """
            INSERT INTO rooms (id, workspace_id, name, time_zone_id, is_active)
            VALUES ($1, $2, $3, $4, $5)
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(room.Id);
        command.Parameters.AddWithValue(room.WorkspaceId);
        command.Parameters.AddWithValue(room.Name);
        command.Parameters.AddWithValue(room.TimeZoneId);
        command.Parameters.AddWithValue(room.IsActive);
        command.ExecuteNonQuery();
    }
}
