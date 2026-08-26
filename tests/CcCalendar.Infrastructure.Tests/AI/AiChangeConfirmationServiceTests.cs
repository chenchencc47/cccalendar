using CcCalendar.Core.AI;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.AI;
using CcCalendar.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class AiChangeConfirmationServiceTests
{
    [Fact]
    public async Task BatchPreviewConfirmAndUndoRestoresModifiedAndRecycledEntities()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid statusTodoId;
        Guid recycledTodoId;
        Guid eventId;
        await using (var seed = database.CreateContext())
        {
            TodoItem statusTodo = TodoItem.Create("Start implementation", null, null);
            TodoItem recycledTodo = TodoItem.Create("Obsolete task", null, null);
            CalendarEvent calendarEvent = CalendarEvent.CreateAllDay(
                "Cancelled meeting",
                null,
                new DateOnly(2026, 8, 18),
                new DateOnly(2026, 8, 19));
            statusTodoId = statusTodo.Id;
            recycledTodoId = recycledTodo.Id;
            eventId = calendarEvent.Id;
            seed.AddRange(statusTodo, recycledTodo, calendarEvent);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var service = new AiChangeConfirmationService(
            database.DatabasePath,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)));
        AiChangePreview preview = service.Preview(new AiChangeSetDraft(
            "Apply planning changes",
            [
                new AiTodoStatusChange(statusTodoId, TodoStatus.InProgress),
                new AiRecycleChange(AiEntityKind.Todo, recycledTodoId),
                new AiRecycleChange(AiEntityKind.Event, eventId),
            ]));

        await using (var before = database.CreateContext())
        {
            Assert.Equal(TodoStatus.Inbox, (await before.Todos.FindAsync(statusTodoId))!.Status);
            Assert.Equal(2, await before.Todos.CountAsync());
            Assert.Single(await before.CalendarEvents.ToListAsync());
        }

        await service.ConfirmAsync(preview.ProposalId, CancellationToken.None);

        await using (var confirmed = database.CreateContext())
        {
            Assert.Equal(TodoStatus.InProgress, (await confirmed.Todos.FindAsync(statusTodoId))!.Status);
            Assert.Single(await confirmed.Todos.ToListAsync());
            Assert.Empty(await confirmed.CalendarEvents.ToListAsync());
            Assert.Equal(3, await confirmed.AuditEntries.CountAsync());
        }

        string? description = await service.UndoLastAsync(CancellationToken.None);

        Assert.Equal("Apply planning changes", description);
        await using var undone = database.CreateContext();
        Assert.Equal(TodoStatus.Inbox, (await undone.Todos.FindAsync(statusTodoId))!.Status);
        Assert.Equal(2, await undone.Todos.CountAsync());
        Assert.Single(await undone.CalendarEvents.ToListAsync());
        Assert.Equal(6, await undone.AuditEntries.CountAsync());
        Assert.NotNull(await undone.Todos.IgnoreQueryFilters().SingleAsync(todo => todo.Id == recycledTodoId));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
