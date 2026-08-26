using CcCalendar.Core.Sync;

namespace CcCalendar.Server;

public interface ISyncChangeStore
{
    SyncChange Append(SyncChange change);

    IReadOnlyList<SyncChange> ReadAfter(Guid workspaceId, long cursor);
}
