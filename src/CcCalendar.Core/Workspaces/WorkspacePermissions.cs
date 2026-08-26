namespace CcCalendar.Core.Workspaces;

public static class WorkspacePermissions
{
    public static bool CanReadRooms(WorkspaceRole role) => role >= WorkspaceRole.Viewer;

    public static bool CanCreateBooking(WorkspaceRole role) => role >= WorkspaceRole.Member;

    public static bool CanManageRooms(WorkspaceRole role) => role >= WorkspaceRole.Admin;
}
