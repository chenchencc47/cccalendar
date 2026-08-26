using Npgsql;

namespace CcCalendar.Server;

public sealed class PostgresDatabase : IDisposable
{
    public PostgresDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DataSource = NpgsqlDataSource.Create(connectionString);
    }

    public NpgsqlDataSource DataSource { get; }

    public void Initialize()
    {
        using NpgsqlCommand command = DataSource.CreateCommand(PostgresSchema.Definition);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        DataSource.Dispose();
    }
}
