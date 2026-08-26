using CcCalendar.Core.Sync;
using Npgsql;

namespace CcCalendar.Server;

public sealed class PostgresSyncChangeStore(PostgresDatabase database) : ISyncChangeStore
{
    public SyncChange Append(SyncChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        using NpgsqlConnection connection = database.DataSource.OpenConnection();
        using NpgsqlTransaction transaction = connection.BeginTransaction();
        const string versionSql = """
            INSERT INTO workspace_sync_versions (workspace_id, version)
            VALUES ($1, 1)
            ON CONFLICT (workspace_id)
            DO UPDATE SET version = workspace_sync_versions.version + 1
            RETURNING version
            """;
        using var versionCommand = new NpgsqlCommand(versionSql, connection, transaction);
        versionCommand.Parameters.AddWithValue(change.WorkspaceId);
        long version = (long)(versionCommand.ExecuteScalar()
            ?? throw new InvalidOperationException("PostgreSQL did not return a sync version."));

        const string changeSql = """
            INSERT INTO sync_changes (workspace_id, version, entity_type, entity_id, operation)
            VALUES ($1, $2, $3, $4, $5)
            """;
        using var changeCommand = new NpgsqlCommand(changeSql, connection, transaction);
        changeCommand.Parameters.AddWithValue(change.WorkspaceId);
        changeCommand.Parameters.AddWithValue(version);
        changeCommand.Parameters.AddWithValue(change.EntityType);
        changeCommand.Parameters.AddWithValue(change.EntityId);
        changeCommand.Parameters.AddWithValue(change.Operation);
        changeCommand.ExecuteNonQuery();
        transaction.Commit();
        return change with { Version = version };
    }

    public IReadOnlyList<SyncChange> ReadAfter(Guid workspaceId, long cursor)
    {
        const string sql = """
            SELECT workspace_id, entity_type, entity_id, operation, version
            FROM sync_changes
            WHERE workspace_id = $1 AND version > $2
            ORDER BY version
            LIMIT 500
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(Math.Max(cursor, 0));
        using NpgsqlDataReader reader = command.ExecuteReader();
        var changes = new List<SyncChange>();
        while (reader.Read())
        {
            changes.Add(new SyncChange(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetInt64(4)));
        }

        return changes;
    }
}
