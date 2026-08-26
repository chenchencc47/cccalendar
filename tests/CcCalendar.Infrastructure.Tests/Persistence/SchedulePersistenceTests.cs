using CcCalendar.Core.Projects;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class SchedulePersistenceTests
{
    [Fact]
    public async Task EventsAndTimeBlocksRoundTrip()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var start = new DateTimeOffset(2026, 8, 17, 14, 0, 0, TimeSpan.FromHours(8));
        Guid timedEventId;
        Guid allDayEventId;
        Guid blockId;

        await using (var writeContext = database.CreateContext())
        {
            Project project = Project.Create("cccalendar", "#246BCE", null, null);
            TodoItem todo = TodoItem.Create("Implement schedule", project.Id, start.AddDays(1));
            CalendarEvent timedEvent = CalendarEvent.CreateTimed(
                "Schedule review",
                project.Id,
                start,
                start.AddHours(1),
                "China Standard Time");
            CalendarEvent allDayEvent = CalendarEvent.CreateAllDay(
                "Release window",
                project.Id,
                new DateOnly(2026, 8, 20),
                new DateOnly(2026, 8, 22));
            TimeBlock block = TimeBlock.Create(todo.Id, start, start.AddHours(2), "China Standard Time");
            timedEventId = timedEvent.Id;
            allDayEventId = allDayEvent.Id;
            blockId = block.Id;

            writeContext.AddRange(project, todo, timedEvent, allDayEvent, block);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        CalendarEvent timedActual = await readContext.CalendarEvents.SingleAsync(item => item.Id == timedEventId);
        CalendarEvent allDayActual = await readContext.CalendarEvents.SingleAsync(item => item.Id == allDayEventId);
        TimeBlock blockActual = await readContext.TimeBlocks.SingleAsync(item => item.Id == blockId);

        Assert.Equal(TimeSpan.FromHours(1), timedActual.Duration);
        Assert.Equal(2, allDayActual.AllDayLength);
        Assert.Equal(TimeSpan.FromHours(2), blockActual.Duration);
    }

    [Fact]
    public async Task EventLocationAndEditsRoundTrip()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid eventId;

        await using (var writeContext = database.CreateContext())
        {
            CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
                "每日例会",
                null,
                new DateTimeOffset(2026, 8, 18, 18, 10, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 18, 19, 10, 0, TimeSpan.FromHours(8)),
                "China Standard Time",
                "项目组二楼会议室");
            eventId = calendarEvent.Id;
            writeContext.Add(calendarEvent);
            await writeContext.SaveChangesAsync(CancellationToken.None);

            calendarEvent.UpdateDetails("每日例会（改期）", "聚英堂会议室");
            calendarEvent.RescheduleTimed(
                new DateTimeOffset(2026, 8, 19, 18, 10, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 19, 19, 10, 0, TimeSpan.FromHours(8)),
                "China Standard Time");
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        CalendarEvent actual = await readContext.CalendarEvents.SingleAsync(item => item.Id == eventId);

        Assert.Equal("每日例会（改期）", actual.Title);
        Assert.Equal("聚英堂会议室", actual.Location);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 19, 10, 10, 0, TimeSpan.Zero),
            actual.StartAtUtc);

        actual.RescheduleAllDay(new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 21));
        await readContext.SaveChangesAsync(CancellationToken.None);

        await using var verifyContext = database.CreateContext();
        CalendarEvent finalActual = await verifyContext.CalendarEvents.SingleAsync(item => item.Id == eventId);

        Assert.True(finalActual.IsAllDay);
        Assert.Equal("聚英堂会议室", finalActual.Location);
    }
}
