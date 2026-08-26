using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

internal sealed class TemporaryCalendarDatabase : IAsyncDisposable
{
    private readonly string databaseDirectory = Path.Combine(
        Path.GetTempPath(),
        "cccalendar-tests",
        Guid.NewGuid().ToString("N"));

    public string DatabasePath => Path.Combine(databaseDirectory, "calendar.db");

    public CalendarDbContext CreateContext()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
        };
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString.ToString())
            .Options;

        return new CalendarDbContext(options);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(databaseDirectory);
        await using CalendarDbContext context = CreateContext();
        await DatabaseInitializer.InitializeAsync(context, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(databaseDirectory))
        {
            Directory.Delete(databaseDirectory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
