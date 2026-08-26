namespace CcCalendar.Core.Sync;

public interface ISyncCursorStore
{
    long Load(Guid workspaceId);

    void Save(Guid workspaceId, long cursor);
}
