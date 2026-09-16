using CcCalendar.Core.Todos;

namespace CcCalendar.Core.QuickAdd;

public sealed record QuickAddRequest(
    QuickAddKind Kind,
    string Title,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? TimeZoneId,
    DateOnly? AllDayStart = null,
    DateOnly? AllDayEndExclusive = null,
    string? Location = null,
    string? MeetingInvitationText = null,
    int? ReminderLeadMinutes = null,
    TodoQuadrant? Quadrant = null);
