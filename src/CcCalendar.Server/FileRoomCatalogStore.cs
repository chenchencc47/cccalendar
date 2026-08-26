using System.Text.Json;
using CcCalendar.Core.Rooms;

namespace CcCalendar.Server;

public sealed class FileRoomCatalogStore : IRoomCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object gate = new();
    private readonly string filePath;
    private readonly List<Room> rooms;

    public FileRoomCatalogStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        filePath = Path.Combine(dataDirectory, "rooms.json");
        rooms = Load();
    }

    public IReadOnlyList<Room> List(Guid workspaceId)
    {
        lock (gate)
        {
            return rooms.Where(room => room.WorkspaceId == workspaceId).ToArray();
        }
    }

    public void Add(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        lock (gate)
        {
            rooms.Add(room);
            Persist();
        }
    }

    private List<Room> Load()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        RoomSnapshot[] snapshots = JsonSerializer.Deserialize<RoomSnapshot[]>(
            File.ReadAllText(filePath),
            JsonOptions) ?? [];
        return snapshots
            .Select(snapshot => Room.Restore(
                snapshot.Id,
                snapshot.WorkspaceId,
                snapshot.Name,
                snapshot.TimeZoneId,
                snapshot.IsActive))
            .ToList();
    }

    private void Persist()
    {
        RoomSnapshot[] snapshots = rooms.Select(room => new RoomSnapshot(
            room.Id,
            room.WorkspaceId,
            room.Name,
            room.TimeZoneId,
            room.IsActive)).ToArray();
        AtomicJsonFile.Write(filePath, snapshots, JsonOptions);
    }

    private sealed record RoomSnapshot(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        string TimeZoneId,
        bool IsActive);
}
