namespace CcCalendar.Core.Sync;

public sealed class SyncOutboxMessage
{
    private SyncOutboxMessage()
    {
        EntityType = null!;
        PayloadJson = null!;
    }

    private SyncOutboxMessage(
        string entityType,
        Guid entityId,
        SyncOperationKind operation,
        string payloadJson,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("An outbox message must reference an entity.", nameof(entityId));
        }

        Id = Guid.NewGuid();
        EntityType = entityType.Trim();
        EntityId = entityId;
        Operation = operation;
        PayloadJson = payloadJson;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public SyncOperationKind Operation { get; private set; }

    public string PayloadJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }

    public static SyncOutboxMessage Enqueue(
        string entityType,
        Guid entityId,
        SyncOperationKind operation,
        string payloadJson,
        DateTimeOffset createdAtUtc)
    {
        return new SyncOutboxMessage(entityType, entityId, operation, payloadJson, createdAtUtc);
    }

    public void MarkAttempt(DateTimeOffset attemptedAtUtc)
    {
        AttemptCount++;
        LastAttemptAtUtc = attemptedAtUtc.ToUniversalTime();
    }

    public void MarkAcknowledged(DateTimeOffset acknowledgedAtUtc)
    {
        AcknowledgedAtUtc = acknowledgedAtUtc.ToUniversalTime();
    }
}
