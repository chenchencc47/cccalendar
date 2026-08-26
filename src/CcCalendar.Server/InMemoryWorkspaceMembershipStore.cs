using System.Collections.Concurrent;
using CcCalendar.Core.Workspaces;

namespace CcCalendar.Server;

public sealed class InMemoryWorkspaceMembershipStore : IWorkspaceMembershipStore
{
    private readonly ConcurrentDictionary<(Guid WorkspaceId, Guid UserId), WorkspaceMembership> memberships = new();

    public WorkspaceMembership? Find(Guid workspaceId, Guid userId)
    {
        memberships.TryGetValue((workspaceId, userId), out WorkspaceMembership? membership);
        return membership;
    }

    public void Upsert(WorkspaceMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        memberships[(membership.WorkspaceId, membership.UserId)] = membership;
    }
}
