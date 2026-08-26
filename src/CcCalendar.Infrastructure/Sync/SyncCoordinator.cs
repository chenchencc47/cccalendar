using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public sealed class SyncCoordinator : IDisposable
{
    private readonly ISyncChangeClient client;
    private readonly ISyncCursorStore cursorStore;
    private readonly Action<SyncChange> applyChange;
    private readonly SemaphoreSlim gate = new(1, 1);
    private SyncConnectionState state = SyncConnectionState.Offline;

    public SyncCoordinator(
        ISyncChangeClient client,
        ISyncCursorStore cursorStore,
        Action<SyncChange> applyChange)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.cursorStore = cursorStore ?? throw new ArgumentNullException(nameof(cursorStore));
        this.applyChange = applyChange ?? throw new ArgumentNullException(nameof(applyChange));
    }

    public SyncConnectionState State => state;

    public void Dispose()
    {
        gate.Dispose();
    }

    public async Task SyncOnceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        state = SyncConnectionState.Syncing;
        try
        {
            long cursor = Math.Max(cursorStore.Load(workspaceId), 0);
            SyncPage page = await client.GetChangesAsync(workspaceId, cursor, cancellationToken);
            foreach (SyncChange change in page.Changes)
            {
                applyChange(change);
            }

            if (page.Cursor >= cursor)
            {
                cursorStore.Save(workspaceId, page.Cursor);
            }

            state = SyncConnectionState.Online;
        }
        catch
        {
            state = SyncConnectionState.Faulted;
            throw;
        }
        finally
        {
            gate.Release();
        }
    }
}
