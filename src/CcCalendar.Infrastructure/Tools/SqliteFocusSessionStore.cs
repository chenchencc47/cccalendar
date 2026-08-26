using CcCalendar.Core.Tools;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tools;

public sealed class SqliteFocusSessionStore : IFocusSessionStore
{
    private readonly string connectionString;

    public SqliteFocusSessionStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        }.ToString();
    }

    public async Task AddAsync(FocusSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using CalendarDbContext context = CreateContext();
        context.FocusSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FocusSession>> LoadAsync(CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        return await context.FocusSessions.AsNoTracking().ToArrayAsync(cancellationToken);
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
