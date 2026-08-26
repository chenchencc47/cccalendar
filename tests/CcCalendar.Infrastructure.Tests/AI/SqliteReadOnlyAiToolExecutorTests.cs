using System.Text.Json;
using CcCalendar.Core.AI;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.AI;
using CcCalendar.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class SqliteReadOnlyAiToolExecutorTests
{
    [Fact]
    public async Task QueryTodosReturnsOnlyMatchingProjectionWithoutUnrelatedRecordContent()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        await using (var context = database.CreateContext())
        {
            context.Add(TodoItem.Create(
                "Release checklist",
                null,
                new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero)));
            context.Add(TodoItem.Create("Personal secret todo", null, null));
            context.Add(WorkRecord.Create(
                WorkRecordType.WorkLog,
                "Unrelated record",
                null,
                "private-record-body",
                new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero)));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var executor = new SqliteReadOnlyAiToolExecutor(database.DatabasePath);
        using JsonDocument arguments = JsonDocument.Parse("{\"query\":\"release\",\"limit\":10}");

        AiToolExecutionResult result = await executor.ExecuteAsync(
            "query_todos",
            arguments.RootElement,
            CancellationToken.None);

        Assert.Contains("Release checklist", result.Json, StringComparison.Ordinal);
        Assert.Contains("Inbox", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("Personal secret todo", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-record-body", result.Json, StringComparison.Ordinal);
        await using var verification = database.CreateContext();
        Assert.Equal(2, await verification.Todos.CountAsync());
        Assert.Single(await verification.WorkRecords.ToListAsync());
    }

    [Fact]
    public async Task QueryEventsWithUtcRangeReturnsOnlyEventsOverlappingTheRange()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        await using (var context = database.CreateContext())
        {
            context.Add(CalendarEvent.CreateTimed(
                "Later meeting",
                null,
                new DateTimeOffset(2026, 8, 18, 10, 10, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 18, 11, 10, 0, TimeSpan.Zero),
                "UTC",
                "Room B"));
            context.Add(CalendarEvent.CreateTimed(
                "Earlier meeting",
                null,
                new DateTimeOffset(2026, 8, 18, 8, 10, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 18, 9, 10, 0, TimeSpan.Zero),
                "UTC",
                "Room A"));
            context.Add(CalendarEvent.CreateTimed(
                "Past planning",
                null,
                new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero),
                "UTC"));
            context.Add(CalendarEvent.CreateAllDay(
                "In range all-day",
                null,
                new DateOnly(2026, 8, 18),
                new DateOnly(2026, 8, 19)));
            context.Add(CalendarEvent.CreateAllDay(
                "Out of range all-day",
                null,
                new DateOnly(2026, 8, 19),
                new DateOnly(2026, 8, 20)));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var executor = new SqliteReadOnlyAiToolExecutor(database.DatabasePath);
        using JsonDocument arguments = JsonDocument.Parse(
            "{\"query\":\"\",\"fromUtc\":\"2026-08-18T00:00:00Z\",\"toUtc\":\"2026-08-19T00:00:00Z\",\"limit\":10}");

        AiToolExecutionResult result = await executor.ExecuteAsync(
            "query_events",
            arguments.RootElement,
            CancellationToken.None);

        Assert.Contains("Earlier meeting", result.Json, StringComparison.Ordinal);
        Assert.Contains("In range all-day", result.Json, StringComparison.Ordinal);
        Assert.Contains("Room A", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("Out of range all-day", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("Past planning", result.Json, StringComparison.Ordinal);
        Assert.True(
            result.Json.IndexOf("Earlier meeting", StringComparison.Ordinal)
                < result.Json.IndexOf("Later meeting", StringComparison.Ordinal));
    }
}
