using CcCalendar.Core.Rooms;

namespace CcCalendar.Infrastructure.Sync;

public sealed class RoomCatalogCache
{
    private IReadOnlyList<RoomCatalogEntry>? rooms;
    private Guid? workspaceId;

    public async Task<IReadOnlyList<RoomCatalogEntry>> GetAsync(
        IRoomBookingClient client,
        Guid requestedWorkspaceId,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (!forceRefresh
            && rooms is not null
            && workspaceId == requestedWorkspaceId)
        {
            return rooms;
        }

        IReadOnlyList<RoomCatalogEntry> loaded = await client.GetRoomsAsync(
            requestedWorkspaceId,
            cancellationToken);
        workspaceId = requestedWorkspaceId;
        rooms = loaded.ToArray();
        return rooms;
    }
}
