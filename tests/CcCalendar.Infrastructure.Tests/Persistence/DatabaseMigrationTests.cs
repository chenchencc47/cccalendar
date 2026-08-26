using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task InitializeAppliesBootstrapMigrationToARealFile()
    {
        string databasePath;

        await using (var database = new TemporaryCalendarDatabase())
        {
            databasePath = database.DatabasePath;

            await database.InitializeAsync(CancellationToken.None);
            await using var context = database.CreateContext();
            string[] migrations = [.. await context.Database.GetAppliedMigrationsAsync()];

            Assert.True(File.Exists(databasePath));
            Assert.Contains(migrations, migration => migration.EndsWith("_Bootstrap", StringComparison.Ordinal));
        }

        Assert.False(File.Exists(databasePath));
    }
}
