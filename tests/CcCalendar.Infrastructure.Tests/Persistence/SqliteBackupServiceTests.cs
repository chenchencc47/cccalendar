using CcCalendar.Core.Projects;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class SqliteBackupServiceTests
{
    [Fact]
    public async Task BackupAndRestoreRecoverDatabaseContents()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid projectId;

        await using (var createContext = database.CreateContext())
        {
            Project project = Project.Create("cccalendar", "#246BCE", null, null);
            projectId = project.Id;
            createContext.Projects.Add(project);
            await createContext.SaveChangesAsync(CancellationToken.None);
        }

        string backupDirectory = Path.Combine(Path.GetDirectoryName(database.DatabasePath)!, "backups");
        string backupPath = SqliteBackupService.CreateBackup(
            database.DatabasePath,
            backupDirectory,
            new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero));

        await using (var deleteContext = database.CreateContext())
        {
            Project project = await deleteContext.Projects.SingleAsync(item => item.Id == projectId);
            deleteContext.Projects.Remove(project);
            await deleteContext.SaveChangesAsync(CancellationToken.None);
        }

        SqliteBackupService.RestoreBackup(backupPath, database.DatabasePath);

        await using var restoredContext = database.CreateContext();
        Assert.True(await restoredContext.Projects.AnyAsync(item => item.Id == projectId));
        Assert.Equal(DatabaseFormat.CurrentVersion, SqliteBackupService.ReadFormatVersion(database.DatabasePath));
    }
}
