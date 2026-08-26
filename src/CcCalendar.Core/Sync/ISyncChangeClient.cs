namespace CcCalendar.Core.Sync;

public interface ISyncChangeClient
{
    Task<SyncPage> GetChangesAsync(
        Guid workspaceId,
        long cursor,
        CancellationToken cancellationToken);
}
