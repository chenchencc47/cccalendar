using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Core.Tools;

namespace CcCalendar.Infrastructure.Persistence;

public sealed record CalendarDataSnapshot(
    IReadOnlyList<Project> Projects,
    IReadOnlyList<TodoItem> Todos,
    IReadOnlyList<CalendarEvent> CalendarEvents,
    IReadOnlyList<WorkRecord> Records,
    IReadOnlyList<FocusSession> FocusSessions);
