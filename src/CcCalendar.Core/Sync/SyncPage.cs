namespace CcCalendar.Core.Sync;

public sealed record SyncPage(
    long Cursor,
    IReadOnlyList<SyncChange> Changes);
