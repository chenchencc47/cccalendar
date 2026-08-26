using CcCalendar.Core.Sync;

namespace CcCalendar.Core.Tests.Sync;

public sealed class SyncMetadataTests
{
    [Fact]
    public void MetadataCreatesWithStableDeviceIdentityAndEmptyCursor()
    {
        Guid deviceId = Guid.NewGuid();
        DateTimeOffset createdAt = new(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);

        SyncMetadata metadata = SyncMetadata.Create(deviceId, createdAt);

        Assert.Equal(deviceId, metadata.DeviceId);
        Assert.Null(metadata.PullCursor);
        Assert.Equal(createdAt, metadata.UpdatedAtUtc);
    }

    [Fact]
    public void OutboxMessageNormalizesIdentityAndOperationData()
    {
        Guid entityId = Guid.NewGuid();
        DateTimeOffset createdAt = new(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);

        SyncOutboxMessage message = SyncOutboxMessage.Enqueue(
            " CalendarEvent ",
            entityId,
            SyncOperationKind.Created,
            "{\"title\":\"评审\"}",
            createdAt);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal("CalendarEvent", message.EntityType);
        Assert.Equal(entityId, message.EntityId);
        Assert.Equal(SyncOperationKind.Created, message.Operation);
        Assert.Equal("{\"title\":\"评审\"}", message.PayloadJson);
        Assert.Equal(0, message.AttemptCount);
        Assert.Null(message.AcknowledgedAtUtc);
    }
}
