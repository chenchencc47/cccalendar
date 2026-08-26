namespace CcCalendar.Core.Workspaces;

public sealed class WorkspaceMembership
{
    private WorkspaceMembership()
    {
    }

    private WorkspaceMembership(Guid workspaceId, Guid userId, WorkspaceRole role, string? displayName)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A membership must reference a workspace.", nameof(workspaceId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A membership must reference a user.", nameof(userId));
        }

        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public static WorkspaceMembership Create(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role,
        string? displayName = null)
    {
        return new WorkspaceMembership(workspaceId, userId, role, displayName);
    }
}
