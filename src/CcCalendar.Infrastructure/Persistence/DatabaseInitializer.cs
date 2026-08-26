using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        CalendarDbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await context.Database.MigrateAsync(cancellationToken);
        // 保证从 0.3.4 之前创建的 SQLite 文件也能无损升级；迁移历史缺少列时按列幂等补齐。
        await AddColumnIfMissingAsync(context, "MeetingInvitationText", "TEXT", cancellationToken);
        await AddColumnIfMissingAsync(context, "MeetingInvitationExpiresOn", "TEXT", cancellationToken);
        await AddColumnIfMissingAsync(context, "ReminderLeadMinutes", "INTEGER", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA user_version = 1;", cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(
        CalendarDbContext context,
        string columnName,
        string columnType,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info('CalendarEvents');";
        await context.Database.OpenConnectionAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (!columns.Contains(columnName))
        {
            await using var alter = context.Database.GetDbConnection().CreateCommand();
            alter.CommandText = $"ALTER TABLE CalendarEvents ADD COLUMN {columnName} {columnType};";
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }

        await context.Database.CloseConnectionAsync();
    }
}
