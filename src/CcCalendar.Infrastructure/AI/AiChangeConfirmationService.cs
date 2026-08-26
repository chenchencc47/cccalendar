using CcCalendar.Core.AI;
using CcCalendar.Core.Auditing;
using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.AI;

public sealed class AiChangeConfirmationService : IAiChangeConfirmationService
{
    private readonly string connectionString;
    private readonly Dictionary<Guid, AiChangeSetDraft> pending = [];
    private readonly object pendingLock = new();
    private readonly TimeProvider timeProvider;
    private readonly UndoManager undoManager = new();

    public AiChangeConfirmationService(
        string databasePath,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        }.ToString();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public AiChangePreview Preview(AiChangeSetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Guid proposalId = Guid.NewGuid();
        lock (pendingLock)
        {
            pending.Add(proposalId, draft);
        }

        return new AiChangePreview(
            proposalId,
            draft.Description,
            draft.Changes.Select(Describe).ToArray());
    }

    public async Task<AiChangeResult> ConfirmAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        AiChangeSetDraft draft;
        lock (pendingLock)
        {
            if (!pending.Remove(proposalId, out draft!))
            {
                throw new KeyNotFoundException("The change proposal does not exist or was already handled.");
            }
        }

        try
        {
            var snapshots = new List<UndoSnapshot>();
            await using CalendarDbContext context = CreateContext();
            foreach (AiChange change in draft.Changes)
            {
                AppliedChange applied = await ApplyAsync(context, change, cancellationToken);
                snapshots.Add(applied.Snapshot);
                context.AuditEntries.Add(AuditEntry.Record(
                    applied.EntityType,
                    applied.EntityId,
                    AuditAction.AppliedAiChange,
                    timeProvider.GetUtcNow(),
                    canUndo: true));
            }

            await context.SaveChangesAsync(cancellationToken);
            undoManager.Register(
                draft.Description,
                token => UndoAsync(snapshots, token));
            return new AiChangeResult(draft.Changes.Count);
        }
        catch
        {
            lock (pendingLock)
            {
                pending.TryAdd(proposalId, draft);
            }

            throw;
        }
    }

    public void Cancel(Guid proposalId)
    {
        lock (pendingLock)
        {
            pending.Remove(proposalId);
        }
    }

    public Task<string?> UndoLastAsync(CancellationToken cancellationToken)
    {
        return undoManager.UndoLastAsync(cancellationToken);
    }

    private async Task<AppliedChange> ApplyAsync(
        CalendarDbContext context,
        AiChange change,
        CancellationToken cancellationToken)
    {
        switch (change)
        {
            case AiTodoStatusChange statusChange:
                {
                    TodoItem todo = await context.Todos.SingleAsync(
                        item => item.Id == statusChange.TodoId,
                        cancellationToken);
                    var snapshot = new TodoStatusSnapshot(
                        todo.Id,
                        todo.Status,
                        todo.CompletedAtUtc);
                    ApplyStatus(todo, statusChange.NewStatus, timeProvider.GetUtcNow());
                    return new AppliedChange(nameof(TodoItem), todo.Id, snapshot);
                }
            case AiRecycleChange recycleChange:
                return await RecycleAsync(context, recycleChange, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    private async Task<AppliedChange> RecycleAsync(
        CalendarDbContext context,
        AiRecycleChange change,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        switch (change.EntityKind)
        {
            case AiEntityKind.Project:
                {
                    Project project = await context.Projects.SingleAsync(
                        item => item.Id == change.EntityId,
                        cancellationToken);
                    project.MoveToRecycleBin(now);
                    return Recycled(nameof(Project), project.Id, change.EntityKind);
                }
            case AiEntityKind.Todo:
                {
                    TodoItem todo = await context.Todos.SingleAsync(
                        item => item.Id == change.EntityId,
                        cancellationToken);
                    todo.MoveToRecycleBin(now);
                    return Recycled(nameof(TodoItem), todo.Id, change.EntityKind);
                }
            case AiEntityKind.Event:
                {
                    CalendarEvent calendarEvent = await context.CalendarEvents.SingleAsync(
                        item => item.Id == change.EntityId,
                        cancellationToken);
                    calendarEvent.MoveToRecycleBin(now);
                    return Recycled(nameof(CalendarEvent), calendarEvent.Id, change.EntityKind);
                }
            case AiEntityKind.Record:
                {
                    WorkRecord record = await context.WorkRecords.SingleAsync(
                        item => item.Id == change.EntityId,
                        cancellationToken);
                    record.MoveToRecycleBin(now);
                    return Recycled(nameof(WorkRecord), record.Id, change.EntityKind);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    private async Task UndoAsync(
        IReadOnlyList<UndoSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        foreach (UndoSnapshot snapshot in snapshots.Reverse())
        {
            switch (snapshot)
            {
                case TodoStatusSnapshot todoStatus:
                    {
                        TodoItem todo = await context.Todos.IgnoreQueryFilters().SingleAsync(
                            item => item.Id == todoStatus.TodoId,
                            cancellationToken);
                        RestoreStatus(todo, todoStatus);
                        context.AuditEntries.Add(AuditEntry.Record(
                            nameof(TodoItem),
                            todo.Id,
                            AuditAction.Updated,
                            timeProvider.GetUtcNow(),
                            canUndo: false));
                        break;
                    }
                case RecycleSnapshot recycle:
                    await RestoreRecycledAsync(context, recycle, cancellationToken);
                    context.AuditEntries.Add(AuditEntry.Record(
                        EntityTypeName(recycle.EntityKind),
                        recycle.EntityId,
                        AuditAction.Restored,
                        timeProvider.GetUtcNow(),
                        canUndo: false));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(snapshots));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task RestoreRecycledAsync(
        CalendarDbContext context,
        RecycleSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        switch (snapshot.EntityKind)
        {
            case AiEntityKind.Project:
                (await context.Projects.IgnoreQueryFilters().SingleAsync(
                    item => item.Id == snapshot.EntityId,
                    cancellationToken)).RestoreFromRecycleBin();
                break;
            case AiEntityKind.Todo:
                (await context.Todos.IgnoreQueryFilters().SingleAsync(
                    item => item.Id == snapshot.EntityId,
                    cancellationToken)).RestoreFromRecycleBin();
                break;
            case AiEntityKind.Event:
                (await context.CalendarEvents.IgnoreQueryFilters().SingleAsync(
                    item => item.Id == snapshot.EntityId,
                    cancellationToken)).RestoreFromRecycleBin();
                break;
            case AiEntityKind.Record:
                (await context.WorkRecords.IgnoreQueryFilters().SingleAsync(
                    item => item.Id == snapshot.EntityId,
                    cancellationToken)).RestoreFromRecycleBin();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(snapshot));
        }
    }

    private static void ApplyStatus(
        TodoItem todo,
        TodoStatus status,
        DateTimeOffset occurredAtUtc)
    {
        if (status == TodoStatus.Completed)
        {
            todo.Complete(occurredAtUtc);
        }
        else
        {
            todo.MoveToStatus(status);
        }
    }

    private static void RestoreStatus(TodoItem todo, TodoStatusSnapshot snapshot)
    {
        if (snapshot.Status == TodoStatus.Completed)
        {
            todo.Complete(snapshot.CompletedAtUtc!.Value);
        }
        else
        {
            todo.MoveToStatus(snapshot.Status);
        }
    }

    private static AppliedChange Recycled(
        string entityType,
        Guid entityId,
        AiEntityKind entityKind)
    {
        return new AppliedChange(entityType, entityId, new RecycleSnapshot(entityKind, entityId));
    }

    private static string Describe(AiChange change)
    {
        return change switch
        {
            AiTodoStatusChange status => $"待办 {status.TodoId} 状态改为 {status.NewStatus}",
            AiRecycleChange recycle => $"删除 {recycle.EntityKind} {recycle.EntityId}",
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
    }

    private static string EntityTypeName(AiEntityKind kind)
    {
        return kind switch
        {
            AiEntityKind.Project => nameof(Project),
            AiEntityKind.Todo => nameof(TodoItem),
            AiEntityKind.Event => nameof(CalendarEvent),
            AiEntityKind.Record => nameof(WorkRecord),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }

    private abstract record UndoSnapshot;

    private sealed record TodoStatusSnapshot(
        Guid TodoId,
        TodoStatus Status,
        DateTimeOffset? CompletedAtUtc) : UndoSnapshot;

    private sealed record RecycleSnapshot(
        AiEntityKind EntityKind,
        Guid EntityId) : UndoSnapshot;

    private sealed record AppliedChange(
        string EntityType,
        Guid EntityId,
        UndoSnapshot Snapshot);
}
