namespace CcCalendar.Core.Sync;

public sealed class SyncMetadata
{
    private SyncMetadata()
    {
    }

    private SyncMetadata(Guid deviceId, DateTimeOffset updatedAtUtc)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("A sync device identity is required.", nameof(deviceId));
        }

        Id = 1;
        DeviceId = deviceId;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public int Id { get; private set; }

    public Guid DeviceId { get; private set; }

    public string? PullCursor { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SyncMetadata Create(Guid deviceId, DateTimeOffset createdAtUtc)
    {
        return new SyncMetadata(deviceId, createdAtUtc);
    }

    public void AdvancePullCursor(string cursor, DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cursor);
        PullCursor = cursor.Trim();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }
}
