namespace CcCalendar.Core.Rooms;

public sealed class Room
{
    private Room()
    {
        Name = null!;
        TimeZoneId = null!;
    }

    private Room(Guid workspaceId, string name, string timeZoneId)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A room must belong to a workspace.", nameof(workspaceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        Name = name.Trim();
        TimeZoneId = timeZoneId.Trim();
    }

    private Room(
        Guid id,
        Guid workspaceId,
        string name,
        string timeZoneId,
        bool isActive)
        : this(workspaceId, name, timeZoneId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A room must have an identifier.", nameof(id));
        }

        Id = id;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; }

    public string TimeZoneId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Room Create(Guid workspaceId, string name, string timeZoneId)
    {
        return new Room(workspaceId, name, timeZoneId);
    }

    public static Room Restore(
        Guid id,
        Guid workspaceId,
        string name,
        string timeZoneId,
        bool isActive)
    {
        return new Room(id, workspaceId, name, timeZoneId, isActive);
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
