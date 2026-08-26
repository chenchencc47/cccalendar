using System.Collections.Concurrent;
using CcCalendar.Core.Rooms;

namespace CcCalendar.Server;

public sealed class InMemoryRoomCatalogStore : IRoomCatalogStore
{
    private readonly ConcurrentDictionary<Guid, List<Room>> rooms = new();

    public IReadOnlyList<Room> List(Guid workspaceId)
    {
        return rooms.TryGetValue(workspaceId, out List<Room>? workspaceRooms)
            ? Snapshot(workspaceRooms)
            : [];
    }

    public void Add(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        List<Room> workspaceRooms = rooms.GetOrAdd(room.WorkspaceId, _ => []);
        lock (workspaceRooms)
        {
            workspaceRooms.Add(room);
        }
    }

    private static Room[] Snapshot(List<Room> workspaceRooms)
    {
        lock (workspaceRooms)
        {
            return workspaceRooms.ToArray();
        }
    }
}
