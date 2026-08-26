using System.Text.Json;
using System.Text.Json.Serialization;
using CcCalendar.Core.AI;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.AI;

public sealed class SqliteReadOnlyAiToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string connectionString;

    public SqliteReadOnlyAiToolExecutor(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
    }

    public Task<AiToolExecutionResult> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return toolName switch
        {
            "query_events" => QueryEventsAsync(arguments, cancellationToken),
            "query_projects" => QueryProjectsAsync(arguments, cancellationToken),
            "query_records" => QueryRecordsAsync(arguments, cancellationToken),
            "query_todos" => QueryTodosAsync(arguments, cancellationToken),
            _ => throw new InvalidOperationException($"Tool '{toolName}' is not allowed."),
        };
    }

    private async Task<AiToolExecutionResult> QueryTodosAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string query = ReadString(arguments, "query");
        int limit = ReadLimit(arguments);
        await using CalendarDbContext context = CreateContext();
        var items = await context.Todos
            .AsNoTracking()
            .Where(todo => query == string.Empty || EF.Functions.Like(todo.Title, $"%{query}%"))
            .OrderBy(todo => todo.Title)
            .Take(limit)
            .Select(todo => new
            {
                todo.Id,
                todo.Title,
                todo.ProjectId,
                todo.StartAtUtc,
                todo.DueAtUtc,
                todo.Status,
                todo.IsImportant,
                todo.IsUrgentOverride,
            })
            .ToArrayAsync(cancellationToken);
        return Serialize(items);
    }

    private async Task<AiToolExecutionResult> QueryProjectsAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string query = ReadString(arguments, "query");
        int limit = ReadLimit(arguments);
        await using CalendarDbContext context = CreateContext();
        var items = await context.Projects
            .AsNoTracking()
            .Where(project => query == string.Empty || EF.Functions.Like(project.Name, $"%{query}%"))
            .OrderBy(project => project.Name)
            .Take(limit)
            .Select(project => new
            {
                project.Id,
                project.Name,
                project.Status,
                project.StartDate,
                project.DueDate,
                IsArchived = project.ArchivedAtUtc != null,
            })
            .ToArrayAsync(cancellationToken);
        return Serialize(items);
    }

    private async Task<AiToolExecutionResult> QueryEventsAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string query = ReadString(arguments, "query");
        int limit = ReadLimit(arguments);
        DateTimeOffset? fromUtc = ReadDateTimeOffset(arguments, "fromUtc");
        DateTimeOffset? toUtc = ReadDateTimeOffset(arguments, "toUtc");
        await using CalendarDbContext context = CreateContext();
        // DateTimeOffset columns use a custom ticks converter, so range comparisons
        // against DateTimeOffset parameters cannot be translated to SQLite; the
        // title filter stays in SQL and the range filter runs in memory.
        IQueryable<CalendarEvent> source = context.CalendarEvents.AsNoTracking();
        if (query.Length > 0)
        {
            source = source.Where(calendarEvent => EF.Functions.Like(calendarEvent.Title, $"%{query}%"));
        }

        var events = (await source
            .Select(calendarEvent => new
            {
                calendarEvent.Id,
                calendarEvent.Title,
                calendarEvent.Location,
                calendarEvent.ProjectId,
                calendarEvent.IsAllDay,
                calendarEvent.StartAtUtc,
                calendarEvent.EndAtUtc,
                calendarEvent.TimeZoneId,
                calendarEvent.AllDayStart,
                calendarEvent.AllDayEndExclusive,
            })
            .ToListAsync(cancellationToken))
            .AsEnumerable();
        if (fromUtc.HasValue)
        {
            DateTimeOffset rangeStart = fromUtc.Value;
            DateOnly fromDate = DateOnly.FromDateTime(rangeStart.UtcDateTime);
            events = events.Where(calendarEvent => calendarEvent.IsAllDay
                ? calendarEvent.AllDayEndExclusive > fromDate
                : calendarEvent.EndAtUtc.HasValue && calendarEvent.EndAtUtc.Value > rangeStart);
        }

        if (toUtc.HasValue)
        {
            DateTimeOffset rangeEnd = toUtc.Value;
            DateOnly toDate = DateOnly.FromDateTime(rangeEnd.UtcDateTime);
            events = events.Where(calendarEvent => calendarEvent.IsAllDay
                ? calendarEvent.AllDayStart < toDate
                : calendarEvent.StartAtUtc.HasValue && calendarEvent.StartAtUtc.Value < rangeEnd);
        }

        return Serialize(events
            .OrderBy(calendarEvent => calendarEvent.IsAllDay
                ? calendarEvent.AllDayStart!.Value.ToDateTime(TimeOnly.MinValue)
                : calendarEvent.StartAtUtc?.UtcDateTime ?? DateTime.MaxValue)
            .ThenBy(calendarEvent => calendarEvent.Title)
            .Take(limit)
            .ToArray());
    }

    private async Task<AiToolExecutionResult> QueryRecordsAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string query = ReadString(arguments, "query");
        int limit = ReadLimit(arguments);
        bool includeContent = ReadBoolean(arguments, "includeContent");
        await using CalendarDbContext context = CreateContext();
        WorkRecord[] records = await context.WorkRecords
            .AsNoTracking()
            .Include(record => record.Versions)
            .Where(record => query == string.Empty
                || EF.Functions.Like(record.Title, $"%{query}%")
                || record.Versions.Any(version => EF.Functions.Like(
                    version.Content,
                    $"%{query}%")))
            .OrderBy(record => record.Title)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        var items = records.Select(record => new
        {
            record.Id,
            record.Title,
            record.Type,
            record.ProjectId,
            record.UpdatedAtUtc,
            Content = includeContent ? Truncate(record.CurrentContent, 1000) : null,
        });
        return Serialize(items);
    }

    private static string ReadString(JsonElement arguments, string propertyName)
    {
        return arguments.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static bool ReadBoolean(JsonElement arguments, string propertyName)
    {
        return arguments.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static DateTimeOffset? ReadDateTimeOffset(
        JsonElement arguments,
        string propertyName)
    {
        string value = ReadString(arguments, propertyName);
        return DateTimeOffset.TryParse(value, out DateTimeOffset result)
            ? result.ToUniversalTime()
            : null;
    }

    private static int ReadLimit(JsonElement arguments)
    {
        int requested = arguments.TryGetProperty("limit", out JsonElement value)
            && value.TryGetInt32(out int parsed)
            ? parsed
            : 20;
        return Math.Clamp(requested, 1, 50);
    }

    private static string Truncate(string value, int maximumLength)
    {
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    private static AiToolExecutionResult Serialize<T>(T value)
    {
        return new AiToolExecutionResult(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
