namespace CcCalendar.Core.Sync;

public sealed record SyncChange(
    Guid WorkspaceId,
    string EntityType,
    Guid EntityId,
    string Operation,
    long Version);
