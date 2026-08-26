using CcCalendar.Core.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CcCalendar.Server;

[Authorize]
public sealed class SyncHub(WorkspaceAuthorizationService authorization) : Hub
{
    public async Task JoinWorkspace(Guid workspaceId)
    {
        if (!authorization.Allows(Context.User!, workspaceId, WorkspacePermissions.CanReadRooms))
        {
            throw new HubException("workspace_access_denied");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(workspaceId));
    }

    public static string GroupName(Guid workspaceId) => $"workspace:{workspaceId:D}";
}
