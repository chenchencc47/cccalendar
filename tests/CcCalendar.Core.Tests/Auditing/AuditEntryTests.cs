using CcCalendar.Core.Auditing;

namespace CcCalendar.Core.Tests.Auditing;

public sealed class AuditEntryTests
{
    [Fact]
    public void RecordStoresMetadataWithoutPayload()
    {
        Guid entityId = Guid.NewGuid();
        var occurredAtUtc = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

        AuditEntry entry = AuditEntry.Record(
            "TodoItem",
            entityId,
            AuditAction.Updated,
            occurredAtUtc,
            canUndo: true);

        Assert.Equal("TodoItem", entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditAction.Updated, entry.Action);
        Assert.Equal(occurredAtUtc, entry.OccurredAtUtc);
        Assert.True(entry.CanUndo);
    }
}
