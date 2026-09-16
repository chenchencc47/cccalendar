using CcCalendar.Core.Projects;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Core.Records;
using CcCalendar.Core.Reminders;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Core.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Persistence;

public sealed class CalendarDataService
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    public CalendarDataService(string databasePath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        };
        connectionString = builder.ToString();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string databasePath = new SqliteConnectionStringBuilder(connectionString).DataSource;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using CalendarDbContext context = CreateContext();
        await DatabaseInitializer.InitializeAsync(context, cancellationToken);
    }

    public async Task<CalendarDataSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        Project[] projects = [.. await context.Projects
            .AsNoTracking()
            .Include(project => project.Milestones)
            .Include(project => project.Participants)
            .ToListAsync(cancellationToken)];
        TodoItem[] todos = [.. await context.Todos
            .AsNoTracking()
            .Include(todo => todo.Subtasks)
            .Include(todo => todo.Dependencies)
            .ToListAsync(cancellationToken)];
        CalendarEvent[] calendarEvents = [.. await context.CalendarEvents.ToListAsync(cancellationToken)];
        DateOnly currentDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        bool invitationsChanged = false;
        foreach (CalendarEvent calendarEvent in calendarEvents)
        {
            string? before = calendarEvent.MeetingInvitationText;
            calendarEvent.ClearExpiredMeetingInvitation(currentDate);
            invitationsChanged |= before is not null && calendarEvent.MeetingInvitationText is null;
        }

        if (invitationsChanged)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        WorkRecord[] records = [.. await context.WorkRecords
            .AsNoTracking()
            .Include(record => record.Versions)
            .Include(record => record.Attachments)
            .ToListAsync(cancellationToken)];
        FocusSession[] focusSessions = [.. await context.FocusSessions
            .AsNoTracking()
            .ToListAsync(cancellationToken)];
        return new CalendarDataSnapshot(projects, todos, calendarEvents, records, focusSessions);
    }

    public async Task QuickAddAsync(QuickAddRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

        object entity = request.Kind switch
        {
            QuickAddKind.Project => Project.Create(request.Title, "#246BCE", null, null),
            QuickAddKind.Todo => CreateTodo(request),
            QuickAddKind.Event => CreateCalendarEvent(request),
            QuickAddKind.Record => WorkRecord.Create(
                WorkRecordType.WorkLog,
                request.Title,
                null,
                string.Empty,
                timeProvider.GetUtcNow()),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

        await using CalendarDbContext context = CreateContext();
        if (entity is CalendarEvent calendarEvent)
        {
            context.CalendarEvents.Add(calendarEvent);
            await ReplaceEventReminderAsync(context, calendarEvent, request.ReminderLeadMinutes, cancellationToken);
        }
        else
        {
            context.Add(entity);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SaveRecordRevisionAsync(
        Guid recordId,
        string content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        await using CalendarDbContext context = CreateContext();
        var current = await context.WorkRecords
            .Where(item => item.Id == recordId)
            .Select(item => new
            {
                item.Id,
                Latest = item.Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => new { version.VersionNumber, version.Content })
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The work record does not exist.");
        if (string.Equals(current.Latest?.Content, content, StringComparison.Ordinal))
        {
            return false;
        }

        DateTimeOffset savedAtUtc = timeProvider.GetUtcNow();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO RecordVersions (Id, VersionNumber, Content, SavedAtUtc, WorkRecordId) VALUES ({Guid.NewGuid()}, {current.Latest?.VersionNumber + 1 ?? 1}, {content}, {savedAtUtc}, {recordId})",
            cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE WorkRecords SET UpdatedAtUtc = {savedAtUtc} WHERE Id = {recordId}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        CalendarEvent calendarEvent = await context.CalendarEvents
            .SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            ?? throw new KeyNotFoundException("The calendar event does not exist.");
        await RemoveEventRemindersAsync(context, calendarEvent.Id, cancellationToken);
        calendarEvent.MoveToRecycleBin(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        Project project = await context.Projects
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken)
            ?? throw new KeyNotFoundException("The project does not exist.");
        project.MoveToRecycleBin(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateEventAsync(ScheduleUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

        try
        {
            await using CalendarDbContext context = CreateContext();
            CalendarEvent calendarEvent = await context.CalendarEvents
                .SingleOrDefaultAsync(item => item.Id == request.EventId, cancellationToken)
                ?? throw new KeyNotFoundException("The calendar event does not exist.");
            calendarEvent.UpdateDetails(request.Title, request.Location);
            if (request.IsAllDay)
            {
                calendarEvent.RescheduleAllDay(
                    request.AllDayStart ?? throw new ArgumentException(
                        "An all-day update requires the start date.", nameof(request)),
                    request.AllDayEndExclusive ?? throw new ArgumentException(
                        "An all-day update requires the exclusive end date.", nameof(request)));
            }
            else
            {
                calendarEvent.RescheduleTimed(
                    request.StartAt ?? throw new ArgumentException(
                        "A timed update requires the start time.", nameof(request)),
                    request.EndAt ?? throw new ArgumentException(
                        "A timed update requires the end time.", nameof(request)),
                    request.TimeZoneId ?? "UTC");
            }

            int? reminderLeadMinutes = request.IsAllDay ? null : request.ReminderLeadMinutes;
            calendarEvent.UpdateReminderLeadMinutes(reminderLeadMinutes);
            await ReplaceEventReminderAsync(context, calendarEvent, reminderLeadMinutes, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"The schedule update is invalid: {exception.Message}",
                exception);
        }
    }

    public async Task CompleteTodoAsync(Guid todoId, CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        TodoItem todo = await context.Todos
            .SingleOrDefaultAsync(item => item.Id == todoId, cancellationToken)
            ?? throw new KeyNotFoundException("The todo does not exist.");
        todo.Complete(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReopenTodoAsync(Guid todoId, CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        TodoItem todo = await context.Todos
            .SingleOrDefaultAsync(item => item.Id == todoId, cancellationToken)
            ?? throw new KeyNotFoundException("The todo does not exist.");
        todo.Reopen();
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveTodoToQuadrantAsync(
        Guid todoId,
        TodoQuadrant quadrant,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        TodoItem todo = await context.Todos
            .SingleOrDefaultAsync(item => item.Id == todoId, cancellationToken)
            ?? throw new KeyNotFoundException("The todo does not exist.");
        todo.MoveToQuadrant(quadrant);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveTodoToStatusAsync(
        Guid todoId,
        TodoStatus status,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        TodoItem todo = await context.Todos
            .SingleOrDefaultAsync(item => item.Id == todoId, cancellationToken)
            ?? throw new KeyNotFoundException("The todo does not exist.");
        todo.MoveToStatus(status);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static CalendarEvent CreateCalendarEvent(QuickAddRequest request)
    {
        if (request.AllDayStart.HasValue && request.AllDayEndExclusive.HasValue)
        {
            return CalendarEvent.CreateAllDay(
                request.Title,
                null,
                request.AllDayStart.Value,
                request.AllDayEndExclusive.Value);
        }

        if (!request.StartAt.HasValue || !request.EndAt.HasValue)
        {
            throw new ArgumentException("A timed event requires start and end times.", nameof(request));
        }

        return CalendarEvent.CreateTimed(
            request.Title,
            null,
            request.StartAt.Value,
            request.EndAt.Value,
            request.TimeZoneId ?? "UTC",
            request.Location,
            request.MeetingInvitationText,
            request.ReminderLeadMinutes);
    }

    private static TodoItem CreateTodo(QuickAddRequest request)
    {
        TodoItem todo = TodoItem.Create(request.Title, null, null);
        if (request.Quadrant.HasValue)
        {
            todo.MoveToQuadrant(request.Quadrant.Value);
        }

        return todo;
    }

    private async Task ReplaceEventReminderAsync(
        CalendarDbContext context,
        CalendarEvent calendarEvent,
        int? reminderLeadMinutes,
        CancellationToken cancellationToken)
    {
        await RemoveEventRemindersAsync(context, calendarEvent.Id, cancellationToken);
        if (reminderLeadMinutes is not { } leadMinutes || calendarEvent.IsAllDay || calendarEvent.StartAtUtc is not { } startAtUtc)
        {
            return;
        }

        context.Reminders.Add(Reminder.Create(
            ReminderTargetType.CalendarEvent,
            calendarEvent.Id,
            calendarEvent.Title,
            startAtUtc.AddMinutes(-leadMinutes),
            timeProvider.GetUtcNow()));
    }

    private static async Task RemoveEventRemindersAsync(
        CalendarDbContext context,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        Reminder[] reminders = [.. await context.Reminders
            .Where(reminder => reminder.TargetType == ReminderTargetType.CalendarEvent
                && reminder.TargetId == eventId)
            .ToListAsync(cancellationToken)];
        context.Reminders.RemoveRange(reminders);
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
