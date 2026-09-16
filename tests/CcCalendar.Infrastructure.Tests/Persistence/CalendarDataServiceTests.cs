using CcCalendar.Core.Projects;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class CalendarDataServiceTests
{
    [Fact]
    public async Task QuickAddPersistsAllSupportedKindsAndReloadsSnapshot()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        var start = new DateTimeOffset(2026, 8, 17, 2, 0, 0, TimeSpan.Zero);

        await service.QuickAddAsync(new QuickAddRequest(QuickAddKind.Project, "Project", null, null, null), CancellationToken.None);
        await service.QuickAddAsync(new QuickAddRequest(QuickAddKind.Todo, "Todo", null, null, null), CancellationToken.None);
        await service.QuickAddAsync(new QuickAddRequest(QuickAddKind.Event, "Event", start, start.AddHours(1), "China Standard Time"), CancellationToken.None);
        await service.QuickAddAsync(new QuickAddRequest(QuickAddKind.Record, "Record", null, null, null), CancellationToken.None);

        CalendarDataSnapshot snapshot = await service.LoadAsync(CancellationToken.None);

        Assert.Single(snapshot.Projects);
        Assert.Single(snapshot.Todos);
        Assert.Single(snapshot.CalendarEvents);
        Assert.Single(snapshot.Records);
    }

    [Fact]
    public async Task SaveRecordRevisionPersistsEditorContent()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Record, "工作记录", null, null, null),
            CancellationToken.None);
        WorkRecord record = Assert.Single((await service.LoadAsync(CancellationToken.None)).Records);

        await service.SaveRecordRevisionAsync(record.Id, "第二版内容", CancellationToken.None);

        WorkRecord saved = Assert.Single((await service.LoadAsync(CancellationToken.None)).Records);
        Assert.Equal("第二版内容", saved.CurrentContent);
        Assert.Equal(2, saved.Versions.Count);
    }

    [Fact]
    public async Task DeleteProjectMovesItOutOfActiveSnapshot()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Project, "待删除项目", null, null, null),
            CancellationToken.None);
        Project project = Assert.Single((await service.LoadAsync(CancellationToken.None)).Projects);

        await service.DeleteProjectAsync(project.Id, CancellationToken.None);

        Assert.Empty((await service.LoadAsync(CancellationToken.None)).Projects);
        await using CalendarDbContext context = database.CreateContext();
        Project deleted = await context.Projects.IgnoreQueryFilters().SingleAsync();
        Assert.NotNull(deleted.DeletedAtUtc);
    }

    [Fact]
    public async Task QuickAddPersistsInvitationTextAndRemovesItOnTheEighthDay()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(
            database.DatabasePath,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 4, 1, 0, 0, TimeSpan.Zero)));
        await service.InitializeAsync(CancellationToken.None);

        await service.QuickAddAsync(
            new QuickAddRequest(
                QuickAddKind.Event,
                "会议",
                new DateTimeOffset(2026, 8, 27, 6, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 27, 7, 0, 0, TimeSpan.Zero),
                "China Standard Time",
                Location: "会议室",
                MeetingInvitationText: "完整邀请文本"),
            CancellationToken.None);

        CalendarEvent calendarEvent = Assert.Single((await service.LoadAsync(CancellationToken.None)).CalendarEvents);
        Assert.Null(calendarEvent.MeetingInvitationText);
    }

    [Fact]
    public async Task QuickAddMeetingWithReminderCreatesReminderAtLeadTime()
    {
        await using var database = new TemporaryCalendarDatabase();
        var now = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
        var service = new CalendarDataService(database.DatabasePath, new FixedTimeProvider(now));
        await service.InitializeAsync(CancellationToken.None);

        await service.QuickAddAsync(
            new QuickAddRequest(
                QuickAddKind.Event,
                "项目会议",
                new DateTimeOffset(2026, 8, 27, 2, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 27, 3, 0, 0, TimeSpan.Zero),
                "UTC",
                Location: "会议室",
                ReminderLeadMinutes: 30),
            CancellationToken.None);

        await using CalendarDbContext context = database.CreateContext();
        CalendarEvent calendarEvent = await context.CalendarEvents.SingleAsync();
        CcCalendar.Core.Reminders.Reminder reminder = await context.Reminders.SingleAsync();

        Assert.Equal(30, calendarEvent.ReminderLeadMinutes);
        Assert.Equal(calendarEvent.Id, reminder.TargetId);
        Assert.Equal(calendarEvent.StartAtUtc!.Value.AddMinutes(-30), reminder.TriggerAtUtc);
    }

    [Fact]
    public async Task UpdatingMeetingReminderToDisabledRemovesExistingReminder()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(
                QuickAddKind.Event,
                "项目会议",
                new DateTimeOffset(2026, 8, 27, 2, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 27, 3, 0, 0, TimeSpan.Zero),
                "UTC",
                ReminderLeadMinutes: 15),
            CancellationToken.None);
        CalendarEvent calendarEvent = Assert.Single((await service.LoadAsync(CancellationToken.None)).CalendarEvents);

        await service.UpdateEventAsync(
            new ScheduleUpdateRequest(
                calendarEvent.Id,
                calendarEvent.Title,
                null,
                false,
                calendarEvent.StartAtUtc,
                calendarEvent.EndAtUtc,
                "UTC",
                null,
                null,
                null),
            CancellationToken.None);

        await using CalendarDbContext context = database.CreateContext();
        Assert.Empty(await context.Reminders.ToListAsync());
        Assert.Null((await context.CalendarEvents.SingleAsync()).ReminderLeadMinutes);
    }

    [Fact]
    public async Task QuickAddAllDayAndDeleteMovesEventOutOfActiveSnapshot()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        var date = new DateOnly(2026, 8, 20);
        await service.QuickAddAsync(
            new QuickAddRequest(
                QuickAddKind.Event,
                "No fixed time",
                null,
                null,
                null,
                date,
                date.AddDays(1)),
            CancellationToken.None);
        CcCalendar.Core.Schedules.CalendarEvent calendarEvent =
            Assert.Single((await service.LoadAsync(CancellationToken.None)).CalendarEvents);

        await service.DeleteEventAsync(calendarEvent.Id, CancellationToken.None);

        Assert.Empty((await service.LoadAsync(CancellationToken.None)).CalendarEvents);
        await using CalendarDbContext context = database.CreateContext();
        CcCalendar.Core.Schedules.CalendarEvent deleted = await context.CalendarEvents
            .IgnoreQueryFilters()
            .SingleAsync();
        Assert.NotNull(deleted.DeletedAtUtc);
    }

    [Fact]
    public async Task CompleteTodoPersistsCompletedState()
    {
        await using var database = new TemporaryCalendarDatabase();
        var completedAt = new DateTimeOffset(2026, 8, 17, 9, 5, 0, TimeSpan.Zero);
        var service = new CalendarDataService(
            database.DatabasePath,
            new FixedTimeProvider(completedAt));
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Todo, "Finish report", null, null, null),
            CancellationToken.None);
        TodoItem todo = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);

        await service.CompleteTodoAsync(todo.Id, CancellationToken.None);

        TodoItem completed = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);
        Assert.Equal(TodoStatus.Completed, completed.Status);
        Assert.Equal(completedAt, completed.CompletedAtUtc);
    }

    [Fact]
    public async Task UpdateEventPersistsTitleLocationAndNewTimeRange()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(
                QuickAddKind.Event,
                "每日例会",
                new DateTimeOffset(2026, 8, 17, 2, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 17, 3, 0, 0, TimeSpan.Zero),
                "China Standard Time"),
            CancellationToken.None);
        CcCalendar.Core.Schedules.CalendarEvent calendarEvent =
            Assert.Single((await service.LoadAsync(CancellationToken.None)).CalendarEvents);

        await service.UpdateEventAsync(
            new ScheduleUpdateRequest(
                calendarEvent.Id,
                "每日例会（周一）",
                "项目组二楼会议室",
                IsAllDay: false,
                new DateTimeOffset(2026, 8, 18, 10, 10, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 18, 11, 10, 0, TimeSpan.Zero),
                "China Standard Time",
                null,
                null),
            CancellationToken.None);

        CcCalendar.Core.Schedules.CalendarEvent updated =
            Assert.Single((await service.LoadAsync(CancellationToken.None)).CalendarEvents);
        Assert.Equal("每日例会（周一）", updated.Title);
        Assert.Equal("项目组二楼会议室", updated.Location);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 18, 10, 10, 0, TimeSpan.Zero),
            updated.StartAtUtc);
    }

    [Fact]
    public async Task UpdateEventCanSwitchToAllDay()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(
                QuickAddKind.Event,
                "Daily sync",
                new DateTimeOffset(2026, 8, 17, 2, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 17, 3, 0, 0, TimeSpan.Zero),
                "China Standard Time"),
            CancellationToken.None);
        CcCalendar.Core.Schedules.CalendarEvent calendarEvent =
            Assert.Single((await service.LoadAsync(CancellationToken.None)).CalendarEvents);

        await service.UpdateEventAsync(
            new ScheduleUpdateRequest(
                calendarEvent.Id,
                "Daily sync",
                null,
                IsAllDay: true,
                null,
                null,
                null,
                new DateOnly(2026, 8, 20),
                new DateOnly(2026, 8, 21)),
            CancellationToken.None);

        CcCalendar.Core.Schedules.CalendarEvent updated =
            Assert.Single((await service.LoadAsync(CancellationToken.None)).CalendarEvents);
        Assert.True(updated.IsAllDay);
        Assert.Equal(new DateOnly(2026, 8, 20), updated.AllDayStart);
    }

    [Fact]
    public async Task UpdateEventRejectsUnknownEvent()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateEventAsync(
            new ScheduleUpdateRequest(
                Guid.NewGuid(),
                "Missing",
                null,
                IsAllDay: false,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                "UTC",
                null,
                null),
            CancellationToken.None));
    }

    [Fact]
    public async Task ReopenTodoRestartsAnIncompleteTodo()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Todo, "Finish report", null, null, null),
            CancellationToken.None);
        TodoItem todo = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);
        await service.CompleteTodoAsync(todo.Id, CancellationToken.None);

        await service.ReopenTodoAsync(todo.Id, CancellationToken.None);

        TodoItem reopened = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);
        Assert.Equal(TodoStatus.NotStarted, reopened.Status);
        Assert.Null(reopened.CompletedAtUtc);
    }

    [Fact]
    public async Task MoveTodoToQuadrantPersistsImportanceAndUrgency()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Todo, "Finish report", null, null, null),
            CancellationToken.None);
        TodoItem todo = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);

        await service.MoveTodoToQuadrantAsync(
            todo.Id,
            TodoQuadrant.ImportantUrgent,
            CancellationToken.None);

        TodoItem moved = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);
        Assert.True(moved.IsImportant);
        Assert.True(moved.IsUrgentOverride);
    }

    [Fact]
    public async Task QuickAddTodoWithQuadrantPersistsQuadrantFlags()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);

        await service.QuickAddAsync(
            new QuickAddRequest(
                QuickAddKind.Todo,
                "整理需求清单",
                null,
                null,
                null,
                Quadrant: TodoQuadrant.ImportantNotUrgent),
            CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Todo, "未分类事项", null, null, null),
            CancellationToken.None);

        TodoItem[] todos = [.. (await service.LoadAsync(CancellationToken.None)).Todos];
        TodoItem quadrantTodo = Assert.Single(todos, item => item.Title == "整理需求清单");
        Assert.True(quadrantTodo.IsImportant);
        Assert.False(quadrantTodo.IsUrgentOverride);
        TodoItem plainTodo = Assert.Single(todos, item => item.Title == "未分类事项");
        Assert.False(plainTodo.IsImportant);
        Assert.Null(plainTodo.IsUrgentOverride);
    }

    [Fact]
    public async Task MoveTodoToStatusPersistsStatus()
    {
        await using var database = new TemporaryCalendarDatabase();
        var service = new CalendarDataService(database.DatabasePath);
        await service.InitializeAsync(CancellationToken.None);
        await service.QuickAddAsync(
            new QuickAddRequest(QuickAddKind.Todo, "Finish report", null, null, null),
            CancellationToken.None);
        TodoItem todo = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);

        await service.MoveTodoToStatusAsync(
            todo.Id,
            TodoStatus.InProgress,
            CancellationToken.None);

        TodoItem moved = Assert.Single((await service.LoadAsync(CancellationToken.None)).Todos);
        Assert.Equal(TodoStatus.InProgress, moved.Status);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
