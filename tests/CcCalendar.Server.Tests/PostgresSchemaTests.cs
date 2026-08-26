using CcCalendar.Server;

namespace CcCalendar.Server.Tests;

public sealed class PostgresSchemaTests
{
    [Fact]
    public void SchemaEnforcesIdempotencyAndRoomTimeExclusion()
    {
        string sql = PostgresSchema.Definition;

        Assert.Contains("UNIQUE (workspace_id, idempotency_key)", sql, StringComparison.Ordinal);
        Assert.Contains("EXCLUDE USING gist", sql, StringComparison.Ordinal);
        Assert.Contains("tstzrange(start_at_utc, end_at_utc, '[)') WITH &&", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaPersistsWorkspaceMembershipAndSyncCursor()
    {
        string sql = PostgresSchema.Definition;

        Assert.Contains("workspace_memberships", sql, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY (workspace_id, user_id)", sql, StringComparison.Ordinal);
        Assert.Contains("workspace_sync_versions", sql, StringComparison.Ordinal);
        Assert.Contains("UNIQUE (workspace_id, version)", sql, StringComparison.Ordinal);
    }
}
