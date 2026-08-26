using System.Collections.Concurrent;
using CcCalendar.Core.Sync;

namespace CcCalendar.Server;

public sealed class InMemorySyncChangeStore : ISyncChangeStore
{
    private readonly ConcurrentDictionary<Guid, WorkspaceChanges> workspaces = new();

    public SyncChange Append(SyncChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        WorkspaceChanges workspace = workspaces.GetOrAdd(change.WorkspaceId, _ => new WorkspaceChanges());
        lock (workspace.Gate)
        {
            SyncChange versioned = change with { Version = ++workspace.Version };
            workspace.Changes.Add(versioned);
            return versioned;
        }
    }

    public IReadOnlyList<SyncChange> ReadAfter(Guid workspaceId, long cursor)
    {
        WorkspaceChanges? workspace = workspaces.GetValueOrDefault(workspaceId);
        if (workspace is null)
        {
            return [];
        }

        lock (workspace.Gate)
        {
            return workspace.Changes.Where(change => change.Version > cursor).ToArray();
        }
    }

    private sealed class WorkspaceChanges
    {
        public object Gate { get; } = new();

        public long Version { get; set; }

        public List<SyncChange> Changes { get; } = [];
    }
}
