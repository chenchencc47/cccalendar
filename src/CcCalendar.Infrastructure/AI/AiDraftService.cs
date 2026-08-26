using System.Globalization;
using System.Text;
using CcCalendar.Core.AI;
using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.AI;

public sealed class AiDraftService : IAiDraftService
{
    private readonly string connectionString;

    public AiDraftService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
    }

    public async Task<AiTextDraft> BuildProjectSummaryAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        Project project = await context.Projects
            .AsNoTracking()
            .Include(item => item.Milestones)
            .SingleAsync(item => item.Id == projectId, cancellationToken);
        TodoItem[] todos = await context.Todos
            .AsNoTracking()
            .Where(todo => todo.ProjectId == projectId)
            .OrderBy(todo => todo.Title)
            .ToArrayAsync(cancellationToken);
        WorkRecord[] records = await context.WorkRecords
            .AsNoTracking()
            .Where(record => record.ProjectId == projectId)
            .OrderBy(record => record.Title)
            .ToArrayAsync(cancellationToken);
        var markdown = new StringBuilder();
        markdown.AppendLine(CultureInfo.InvariantCulture, $"# {project.Name}");
        markdown.AppendLine(CultureInfo.InvariantCulture, $"- 状态：{project.Status}");
        markdown.AppendLine(CultureInfo.InvariantCulture, $"- 时间：{FormatDate(project.StartDate)} - {FormatDate(project.DueDate)}");
        markdown.AppendLine(CultureInfo.InvariantCulture, $"- 待办：{todos.Count(todo => todo.Status == TodoStatus.Completed)}/{todos.Length} 已完成");
        AppendSection(markdown, "里程碑", project.Milestones.Select(item => item.Title));
        AppendSection(markdown, "待办", todos.Select(todo => $"[{Mark(todo)}] {todo.Title}"));
        AppendSection(markdown, "相关记录", records.Select(record => record.Title));
        return new AiTextDraft($"{project.Name} 项目总结", markdown.ToString().TrimEnd());
    }

    public async Task<AiTextDraft> BuildDailyPlanAsync(
        DateOnly calendarDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        await using CalendarDbContext context = CreateContext();
        CalendarEvent[] allEvents = await context.CalendarEvents.AsNoTracking().ToArrayAsync(cancellationToken);
        TodoItem[] allTodos = await context.Todos.AsNoTracking().ToArrayAsync(cancellationToken);
        CalendarEvent[] events = allEvents
            .Where(calendarEvent => OccursOn(calendarEvent, calendarDate, timeZone))
            .OrderBy(calendarEvent => calendarEvent.StartAtUtc)
            .ToArray();
        TodoItem[] todos = allTodos
            .Where(todo => todo.Status != TodoStatus.Completed)
            .Where(todo => todo.DueAtUtc.HasValue
                && LocalDate(todo.DueAtUtc.Value, timeZone) <= calendarDate)
            .OrderBy(todo => todo.DueAtUtc)
            .ToArray();
        var markdown = new StringBuilder();
        markdown.AppendLine(CultureInfo.InvariantCulture, $"# {calendarDate:yyyy-MM-dd} 每日计划");
        AppendSection(markdown, "日程", events.Select(item => FormatEvent(item, timeZone)));
        AppendSection(markdown, "待办", todos.Select(todo => $"[ ] {todo.Title}"));
        return new AiTextDraft("每日计划", markdown.ToString().TrimEnd());
    }

    public async Task<AiTextDraft> BuildWeeklyReportAsync(
        DateOnly weekStart,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateOnly weekEndExclusive = weekStart.AddDays(7);
        await using CalendarDbContext context = CreateContext();
        TodoItem[] allTodos = await context.Todos.AsNoTracking().ToArrayAsync(cancellationToken);
        WorkRecord[] allRecords = await context.WorkRecords.AsNoTracking().ToArrayAsync(cancellationToken);
        TodoItem[] completed = allTodos
            .Where(todo => todo.CompletedAtUtc.HasValue)
            .Where(todo => InRange(LocalDate(todo.CompletedAtUtc!.Value, timeZone), weekStart, weekEndExclusive))
            .OrderBy(todo => todo.Title)
            .ToArray();
        WorkRecord[] records = allRecords
            .Where(record => InRange(LocalDate(record.UpdatedAtUtc, timeZone), weekStart, weekEndExclusive))
            .OrderBy(record => record.Title)
            .ToArray();
        TodoItem[] followUps = allTodos
            .Where(todo => todo.Status != TodoStatus.Completed)
            .Where(todo => todo.DueAtUtc.HasValue
                && LocalDate(todo.DueAtUtc.Value, timeZone) < weekEndExclusive)
            .OrderBy(todo => todo.Title)
            .ToArray();
        var markdown = new StringBuilder();
        markdown.AppendLine(CultureInfo.InvariantCulture, $"# {weekStart:yyyy-MM-dd} 周报草稿");
        AppendSection(markdown, "本周完成", completed.Select(todo => todo.Title));
        AppendSection(markdown, "本周记录", records.Select(record => record.Title));
        AppendSection(markdown, "待跟进", followUps.Select(todo => todo.Title));
        return new AiTextDraft("周报草稿", markdown.ToString().TrimEnd());
    }

    private static void AppendSection(
        StringBuilder markdown,
        string heading,
        IEnumerable<string> items)
    {
        markdown.AppendLine();
        markdown.AppendLine(CultureInfo.InvariantCulture, $"## {heading}");
        string[] values = [.. items];
        if (values.Length == 0)
        {
            markdown.AppendLine("- 无");
            return;
        }

        foreach (string item in values)
        {
            markdown.AppendLine(CultureInfo.InvariantCulture, $"- {item}");
        }
    }

    private static bool OccursOn(
        CalendarEvent calendarEvent,
        DateOnly calendarDate,
        TimeZoneInfo timeZone)
    {
        if (calendarEvent.IsAllDay)
        {
            return calendarEvent.AllDayStart <= calendarDate
                && calendarDate < calendarEvent.AllDayEndExclusive;
        }

        DateOnly startDate = LocalDate(calendarEvent.StartAtUtc!.Value, timeZone);
        DateOnly endDate = LocalDate(calendarEvent.EndAtUtc!.Value.AddTicks(-1), timeZone);
        return startDate <= calendarDate && calendarDate <= endDate;
    }

    private static string FormatEvent(CalendarEvent calendarEvent, TimeZoneInfo timeZone)
    {
        if (calendarEvent.IsAllDay)
        {
            return $"全天 {calendarEvent.Title}";
        }

        DateTimeOffset start = TimeZoneInfo.ConvertTime(calendarEvent.StartAtUtc!.Value, timeZone);
        DateTimeOffset end = TimeZoneInfo.ConvertTime(calendarEvent.EndAtUtc!.Value, timeZone);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{start:HH:mm}-{end:HH:mm} {calendarEvent.Title}");
    }

    private static string FormatDate(DateOnly? date)
    {
        return date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "未设置";
    }

    private static string Mark(TodoItem todo)
    {
        return todo.Status == TodoStatus.Completed ? "x" : " ";
    }

    private static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);
    }

    private static bool InRange(DateOnly value, DateOnly start, DateOnly endExclusive)
    {
        return value >= start && value < endExclusive;
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
