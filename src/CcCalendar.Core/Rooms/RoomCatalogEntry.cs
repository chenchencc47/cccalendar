namespace CcCalendar.Core.Rooms;

public sealed record RoomCatalogEntry(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string TimeZoneId,
    bool IsActive);
