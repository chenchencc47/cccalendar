using CcCalendar.Core.Workspaces;

namespace CcCalendar.Core.Tests.Workspaces;

public sealed class WorkspacePermissionTests
{
    [Theory]
    [InlineData(WorkspaceRole.Viewer, true, false, false)]
    [InlineData(WorkspaceRole.Member, true, true, false)]
    [InlineData(WorkspaceRole.Admin, true, true, true)]
    [InlineData(WorkspaceRole.Owner, true, true, true)]
    public void RolePermissionsMatchWorkspaceActions(
        WorkspaceRole role,
        bool canRead,
        bool canBook,
        bool canManageRooms)
    {
        Assert.Equal(canRead, WorkspacePermissions.CanReadRooms(role));
        Assert.Equal(canBook, WorkspacePermissions.CanCreateBooking(role));
        Assert.Equal(canManageRooms, WorkspacePermissions.CanManageRooms(role));
    }

    [Fact]
    public void MembershipRequiresWorkspaceAndUserIdentity()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        WorkspaceMembership membership = WorkspaceMembership.Create(
            workspaceId,
            userId,
            WorkspaceRole.Member);

        Assert.Equal(workspaceId, membership.WorkspaceId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(WorkspaceRole.Member, membership.Role);
        Assert.Throws<ArgumentException>(() => WorkspaceMembership.Create(
            Guid.Empty,
            userId,
            WorkspaceRole.Member));
    }
}
