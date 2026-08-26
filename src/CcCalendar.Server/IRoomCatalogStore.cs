using CcCalendar.Core.Rooms;

namespace CcCalendar.Server;

public interface IRoomCatalogStore
{
    IReadOnlyList<Room> List(Guid workspaceId);

    void Add(Room room);
}
