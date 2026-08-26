using CcCalendar.Core.Workspaces;

namespace CcCalendar.Server;

public interface IWorkspaceMembershipStore
{
    WorkspaceMembership? Find(Guid workspaceId, Guid userId);

    void Upsert(WorkspaceMembership membership);
}
