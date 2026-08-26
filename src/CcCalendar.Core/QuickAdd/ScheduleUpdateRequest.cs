namespace CcCalendar.Core.QuickAdd;

public sealed record ScheduleUpdateRequest(
    Guid EventId,
    string Title,
    string? Location,
    bool IsAllDay,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? TimeZoneId,
    DateOnly? AllDayStart,
    DateOnly? AllDayEndExclusive,
    int? ReminderLeadMinutes = null);
