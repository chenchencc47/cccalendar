using CcCalendar.Core.AI;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.AI;
using CcCalendar.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class AiPlanningServiceTests
{
    [Fact]
    public async Task DetectsConflictsFindsWorkingHourSlotsAndConfirmsTaskDecomposition()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid todoId;
        await using (var seed = database.CreateContext())
        {
            CalendarEvent busy = CalendarEvent.CreateTimed(
                "Busy meeting",
                null,
                new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.FromHours(8)),
                "China Standard Time");
            TodoItem todo = TodoItem.Create("Ship release", null, null);
            todoId = todo.Id;
            seed.AddRange(busy, todo);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var service = new AiPlanningService(
            database.DatabasePath,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)));
        AiCreationDraft candidate = AiCreationDraft.TimedEvent(
            "Candidate",
            new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 17, 10, 30, 0, TimeSpan.FromHours(8)),
            "China Standard Time");

        AiConflictPrompt conflict = await service.CheckConflictAsync(candidate, CancellationToken.None);
        IReadOnlyList<AiAvailableSlot> slots = await service.FindAvailableSlotsAsync(
            new DateOnly(2026, 8, 17),
            TimeSpan.FromHours(1),
            "China Standard Time",
            CancellationToken.None);
        AiTaskDecompositionPreview decomposition = service.PreviewTaskDecomposition(
            new AiTaskDecompositionDraft(todoId, ["Build package", "Publish release"]));

        Assert.True(conflict.HasConflict);
        Assert.Equal(1, conflict.ConflictCount);
        Assert.Equal(
            [AiConflictChoice.KeepOverlap, AiConflictChoice.FindAvailableTime, AiConflictChoice.Modify],
            conflict.Choices);
        Assert.Equal(new TimeOnly(8, 0), slots[0].LocalStart);
        Assert.Equal(new TimeOnly(9, 0), slots[0].LocalEnd);
        Assert.DoesNotContain(slots, slot => slot.LocalStart < new TimeOnly(14, 0) && slot.LocalEnd > new TimeOnly(12, 0));

        await using (var before = database.CreateContext())
        {
            Assert.Empty((await before.Todos.Include(todo => todo.Subtasks).SingleAsync()).Subtasks);
        }

        await service.ConfirmTaskDecompositionAsync(
            decomposition.ProposalId,
            CancellationToken.None);

        await using var after = database.CreateContext();
        TodoItem actual = await after.Todos.Include(todo => todo.Subtasks).SingleAsync();
        Assert.Equal(
            ["Build package", "Publish release"],
            actual.Subtasks.Select(subtask => subtask.Title).Order());
        Assert.Single(await after.AuditEntries.ToListAsync());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
