using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CcCalendar.Infrastructure.Persistence;

public static class SqliteBackupService
{
    public static string CreateBackup(
        string databasePath,
        string backupDirectory,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);

        string sourcePath = Path.GetFullPath(databasePath);
        ValidateDatabase(sourcePath);
        Directory.CreateDirectory(backupDirectory);

        string fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"cccalendar-{createdAtUtc.ToUniversalTime():yyyyMMdd-HHmmss}.db");
        string backupPath = Path.Combine(Path.GetFullPath(backupDirectory), fileName);

        if (File.Exists(backupPath))
        {
            throw new IOException("A backup with the same timestamp already exists.");
        }

        BackupDatabase(sourcePath, backupPath);
        ValidateDatabase(backupPath);
        return backupPath;
    }

    public static void RestoreBackup(string backupPath, string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        string sourcePath = Path.GetFullPath(backupPath);
        string destinationPath = Path.GetFullPath(databasePath);
        ValidateDatabase(sourcePath);

        string destinationDirectory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(destinationDirectory);
        string temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.restore";

        try
        {
            BackupDatabase(sourcePath, temporaryPath);
            ValidateDatabase(temporaryPath);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public static int ReadFormatVersion(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        using SqliteConnection connection = OpenReadOnly(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void BackupDatabase(string sourcePath, string destinationPath)
    {
        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };

        using var source = new SqliteConnection(sourceBuilder.ToString());
        using var destination = new SqliteConnection(destinationBuilder.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static SqliteConnection OpenReadOnly(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void ValidateDatabase(string databasePath)
    {
        using SqliteConnection connection = OpenReadOnly(databasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        string? result = command.ExecuteScalar() as string;

        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The SQLite database failed its integrity check.");
        }

        command.CommandText = "PRAGMA user_version;";
        int version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (version is <= 0 or > DatabaseFormat.CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported database format version: {version}.");
        }
    }
}
