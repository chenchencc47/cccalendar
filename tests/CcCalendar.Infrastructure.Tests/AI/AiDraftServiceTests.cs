using CcCalendar.Core.AI;
using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.AI;
using CcCalendar.Infrastructure.Tests.Persistence;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class AiDraftServiceTests
{
    [Fact]
    public async Task BuildsScopedProjectDailyAndWeeklyMarkdownDrafts()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid projectId;
        await using (var seed = database.CreateContext())
        {
            Project project = Project.Create(
                "Alpha launch",
                "#246BCE",
                new DateOnly(2026, 8, 10),
                new DateOnly(2026, 8, 21));
            Project unrelated = Project.Create("Secret beta", "#C43D4B", null, null);
            projectId = project.Id;
            TodoItem pending = TodoItem.Create(
                "Prepare announcement",
                project.Id,
                new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));
            TodoItem completed = TodoItem.Create("Build package", project.Id, null);
            completed.Complete(new DateTimeOffset(2026, 8, 15, 8, 0, 0, TimeSpan.Zero));
            CalendarEvent calendarEvent = CalendarEvent.CreateTimed(
                "Launch review",
                project.Id,
                new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 17, 11, 0, 0, TimeSpan.FromHours(8)),
                "China Standard Time");
            CalendarEvent spanningEvent = CalendarEvent.CreateTimed(
                "Multi-day focus",
                project.Id,
                new DateTimeOffset(2026, 8, 16, 23, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 18, 2, 0, 0, TimeSpan.FromHours(8)),
                "China Standard Time");
            WorkRecord record = WorkRecord.Create(
                WorkRecordType.WorkLog,
                "Launch decision",
                project.Id,
                "Approved staged rollout",
                new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero));
            seed.AddRange(project, unrelated, pending, completed, calendarEvent, spanningEvent, record);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var service = new AiDraftService(database.DatabasePath);

        AiTextDraft projectDraft = await service.BuildProjectSummaryAsync(projectId, CancellationToken.None);
        AiTextDraft daily = await service.BuildDailyPlanAsync(
            new DateOnly(2026, 8, 17),
            "China Standard Time",
            CancellationToken.None);
        AiTextDraft weekly = await service.BuildWeeklyReportAsync(
            new DateOnly(2026, 8, 10),
            "China Standard Time",
            CancellationToken.None);

        Assert.Contains("Alpha launch", projectDraft.Markdown, StringComparison.Ordinal);
        Assert.Contains("Prepare announcement", projectDraft.Markdown, StringComparison.Ordinal);
        Assert.Contains("Launch decision", projectDraft.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret beta", projectDraft.Markdown, StringComparison.Ordinal);
        Assert.Contains("Launch review", daily.Markdown, StringComparison.Ordinal);
        Assert.Contains("Multi-day focus", daily.Markdown, StringComparison.Ordinal);
        Assert.Contains("Prepare announcement", daily.Markdown, StringComparison.Ordinal);
        Assert.Contains("Build package", weekly.Markdown, StringComparison.Ordinal);
        Assert.Contains("本周完成", weekly.Markdown, StringComparison.Ordinal);
    }
}
