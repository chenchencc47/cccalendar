using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CcCalendar.Infrastructure.Persistence;

public sealed class CalendarDbContextFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    public CalendarDbContext CreateDbContext(string[] args)
    {
        string databasePath = Path.Combine(Path.GetTempPath(), "cccalendar-design.db");
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new CalendarDbContext(options);
    }
}
