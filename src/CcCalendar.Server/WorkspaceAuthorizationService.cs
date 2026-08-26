using System.Security.Claims;
using CcCalendar.Core.Workspaces;

namespace CcCalendar.Server;

public sealed class WorkspaceAuthorizationService(IWorkspaceMembershipStore memberships)
{
    public bool Allows(
        ClaimsPrincipal principal,
        Guid workspaceId,
        Func<WorkspaceRole, bool> permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(permission);

        string? subject = principal.FindFirstValue("sub");
        return Guid.TryParse(subject, out Guid userId)
            && memberships.Find(workspaceId, userId) is { } membership
            && permission(membership.Role);
    }
}
