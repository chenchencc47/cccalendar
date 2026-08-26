using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Infrastructure.Tests.Sync;

public sealed class SyncCoordinatorTests
{
    [Fact]
    public async Task AppliesPageBeforeAdvancingCursor()
    {
        Guid workspaceId = Guid.NewGuid();
        var client = new FakeSyncChangeClient(
            new SyncPage(
                7,
                [new SyncChange(workspaceId, "room", Guid.NewGuid(), "created", 7)]));
        var cursorStore = new MemoryCursorStore();
        var applied = new List<SyncChange>();
        var coordinator = new SyncCoordinator(client, cursorStore, applied.Add);

        await coordinator.SyncOnceAsync(workspaceId, CancellationToken.None);

        Assert.Equal(7, cursorStore.Get(workspaceId));
        Assert.Single(applied);
        Assert.Equal(SyncConnectionState.Online, coordinator.State);
    }

    [Fact]
    public async Task FailedApplyDoesNotAdvanceCursorAndCanRetry()
    {
        Guid workspaceId = Guid.NewGuid();
        var client = new FakeSyncChangeClient(
            new SyncPage(
                9,
                [new SyncChange(workspaceId, "room", Guid.NewGuid(), "created", 9)]));
        var cursorStore = new MemoryCursorStore();
        int attempts = 0;
        var coordinator = new SyncCoordinator(
            client,
            cursorStore,
            _ =>
            {
                if (++attempts == 1)
                {
                    throw new InvalidOperationException("apply failed");
                }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.SyncOnceAsync(workspaceId, CancellationToken.None));
        Assert.Equal(0, cursorStore.Get(workspaceId));
        Assert.Equal(SyncConnectionState.Faulted, coordinator.State);

        await coordinator.SyncOnceAsync(workspaceId, CancellationToken.None);

        Assert.Equal(9, cursorStore.Get(workspaceId));
        Assert.Equal(SyncConnectionState.Online, coordinator.State);
    }

    private sealed class FakeSyncChangeClient(SyncPage page) : ISyncChangeClient
    {
        public Task<SyncPage> GetChangesAsync(Guid workspaceId, long cursor, CancellationToken cancellationToken)
        {
            return Task.FromResult(page);
        }
    }

    private sealed class MemoryCursorStore : ISyncCursorStore
    {
        private readonly Dictionary<Guid, long> cursors = [];

        public long Get(Guid workspaceId) => cursors.GetValueOrDefault(workspaceId);

        public long Load(Guid workspaceId) => Get(workspaceId);

        public void Save(Guid workspaceId, long cursor) => cursors[workspaceId] = cursor;
    }
}
