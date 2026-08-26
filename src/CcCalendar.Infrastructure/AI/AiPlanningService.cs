using CcCalendar.Core.AI;
using CcCalendar.Core.Auditing;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Scheduling;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.AI;

public sealed class AiPlanningService : IAiPlanningService
{
    private static readonly IReadOnlyList<AiConflictChoice> ConflictChoices =
    [
        AiConflictChoice.KeepOverlap,
        AiConflictChoice.FindAvailableTime,
        AiConflictChoice.Modify,
    ];

    private readonly string connectionString;
    private readonly Dictionary<Guid, AiTaskDecompositionDraft> pendingDecompositions = [];
    private readonly TimeProvider timeProvider;

    public AiPlanningService(string databasePath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        }.ToString();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AiConflictPrompt> CheckConflictAsync(
        AiCreationDraft candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Kind != AiCreationKind.Event || candidate.IsAllDay)
        {
            return new AiConflictPrompt(false, 0, []);
        }

        var candidateRange = new TimeRange(candidate.StartAt!.Value, candidate.EndAt!.Value);
        IReadOnlyList<TimeRange> busy = await LoadTimedBusyRangesAsync(cancellationToken);
        int conflictCount = ConflictDetector.FindOverlaps(candidateRange, busy).Count;
        return new AiConflictPrompt(
            conflictCount > 0,
            conflictCount,
            conflictCount > 0 ? ConflictChoices : []);
    }

    public async Task<IReadOnlyList<AiAvailableSlot>> FindAvailableSlotsAsync(
        DateOnly calendarDate,
        TimeSpan duration,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        await using CalendarDbContext context = CreateContext();
        CalendarEvent[] events = await context.CalendarEvents.AsNoTracking().ToArrayAsync(cancellationToken);
        TimeBlock[] timeBlocks = await context.TimeBlocks.AsNoTracking().ToArrayAsync(cancellationToken);
        var busy = new List<LocalTimeRange>();
        foreach (CalendarEvent calendarEvent in events)
        {
            if (calendarEvent.IsAllDay)
            {
                if (calendarEvent.AllDayStart <= calendarDate
                    && calendarDate < calendarEvent.AllDayEndExclusive)
                {
                    busy.AddRange(WorkingSchedule.CreateDefault().GetWorkingRanges(calendarDate));
                }

                continue;
            }

            AddLocalBusyRange(
                busy,
                calendarDate,
                calendarEvent.StartAtUtc!.Value,
                calendarEvent.EndAtUtc!.Value,
                timeZone);
        }

        foreach (TimeBlock timeBlock in timeBlocks)
        {
            AddLocalBusyRange(
                busy,
                calendarDate,
                timeBlock.StartAtUtc,
                timeBlock.EndAtUtc,
                timeZone);
        }

        IReadOnlyList<LocalTimeRange> free = WorkingSchedule.CreateDefault().FindFreeRanges(
            calendarDate,
            busy);
        return free
            .Where(range => range.EndTime.ToTimeSpan() - range.StartTime.ToTimeSpan() >= duration)
            .Select(range => CreateSlot(calendarDate, range.StartTime, duration, timeZone))
            .ToArray();
    }

    public AiTaskDecompositionPreview PreviewTaskDecomposition(
        AiTaskDecompositionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Guid proposalId = Guid.NewGuid();
        pendingDecompositions.Add(proposalId, draft);
        return new AiTaskDecompositionPreview(
            proposalId,
            draft.TodoId,
            draft.SubtaskTitles);
    }

    public async Task ConfirmTaskDecompositionAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        if (!pendingDecompositions.Remove(proposalId, out AiTaskDecompositionDraft? draft))
        {
            throw new KeyNotFoundException("The task decomposition does not exist or was already handled.");
        }

        try
        {
            await using CalendarDbContext context = CreateContext();
            TodoItem todo = await context.Todos.Include(item => item.Subtasks).SingleAsync(
                item => item.Id == draft.TodoId,
                cancellationToken);
            foreach (string title in draft.SubtaskTitles)
            {
                context.Add(todo.AddSubtask(title));
            }

            context.AuditEntries.Add(AuditEntry.Record(
                nameof(TodoItem),
                todo.Id,
                AuditAction.AppliedAiChange,
                timeProvider.GetUtcNow(),
                canUndo: true));
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            pendingDecompositions.TryAdd(proposalId, draft);
            throw;
        }
    }

    public void CancelTaskDecomposition(Guid proposalId)
    {
        pendingDecompositions.Remove(proposalId);
    }

    private async Task<IReadOnlyList<TimeRange>> LoadTimedBusyRangesAsync(
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        var ranges = new List<TimeRange>();
        ranges.AddRange((await context.CalendarEvents
            .AsNoTracking()
            .Where(calendarEvent => !calendarEvent.IsAllDay)
            .ToArrayAsync(cancellationToken))
            .Select(calendarEvent => new TimeRange(
                calendarEvent.StartAtUtc!.Value,
                calendarEvent.EndAtUtc!.Value)));
        ranges.AddRange((await context.TimeBlocks
            .AsNoTracking()
            .ToArrayAsync(cancellationToken))
            .Select(timeBlock => new TimeRange(timeBlock.StartAtUtc, timeBlock.EndAtUtc)));
        return ranges;
    }

    private static void AddLocalBusyRange(
        List<LocalTimeRange> busy,
        DateOnly targetDate,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        TimeZoneInfo timeZone)
    {
        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(startAtUtc, timeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(endAtUtc, timeZone);
        DateOnly startDate = DateOnly.FromDateTime(localStart.DateTime);
        DateOnly endDate = DateOnly.FromDateTime(localEnd.DateTime);
        if (endDate < targetDate || startDate > targetDate)
        {
            return;
        }

        TimeOnly start = startDate < targetDate
            ? TimeOnly.MinValue
            : TimeOnly.FromDateTime(localStart.DateTime);
        TimeOnly end = endDate > targetDate
            ? TimeOnly.MaxValue
            : TimeOnly.FromDateTime(localEnd.DateTime);
        if (end > start)
        {
            busy.Add(new LocalTimeRange(targetDate, start, end));
        }
    }

    private static AiAvailableSlot CreateSlot(
        DateOnly date,
        TimeOnly localStart,
        TimeSpan duration,
        TimeZoneInfo timeZone)
    {
        TimeOnly localEnd = localStart.Add(duration);
        DateTime startDateTime = date.ToDateTime(localStart, DateTimeKind.Unspecified);
        DateTime endDateTime = date.ToDateTime(localEnd, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(startDateTime, timeZone.GetUtcOffset(startDateTime));
        var end = new DateTimeOffset(endDateTime, timeZone.GetUtcOffset(endDateTime));
        return new AiAvailableSlot(
            date,
            localStart,
            localEnd,
            start.ToUniversalTime(),
            end.ToUniversalTime());
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
