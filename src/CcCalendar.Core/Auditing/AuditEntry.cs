namespace CcCalendar.Core.Auditing;

public sealed class AuditEntry
{
    private AuditEntry()
    {
        EntityType = null!;
    }

    private AuditEntry(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset occurredAtUtc,
        bool canUndo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("An audit entry must reference an entity.", nameof(entityId));
        }

        Id = Guid.NewGuid();
        EntityType = entityType.Trim();
        EntityId = entityId;
        Action = action;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        CanUndo = canUndo;
    }

    public Guid Id { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public AuditAction Action { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public bool CanUndo { get; private set; }

    public static AuditEntry Record(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset occurredAtUtc,
        bool canUndo)
    {
        return new AuditEntry(entityType, entityId, action, occurredAtUtc, canUndo);
    }
}
