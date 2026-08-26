using CcCalendar.Core.Workspaces;
using Npgsql;

namespace CcCalendar.Server;

public sealed class PostgresWorkspaceMembershipStore(PostgresDatabase database) : IWorkspaceMembershipStore
{
    public WorkspaceMembership? Find(Guid workspaceId, Guid userId)
    {
        const string sql = """
            SELECT role, display_name
            FROM workspace_memberships
            WHERE workspace_id = $1 AND user_id = $2
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(workspaceId);
        command.Parameters.AddWithValue(userId);
        using NpgsqlDataReader reader = command.ExecuteReader();
        return reader.Read()
            && reader.GetString(0) is string role
            && Enum.TryParse(role, ignoreCase: true, out WorkspaceRole parsedRole)
            ? WorkspaceMembership.Create(
                workspaceId,
                userId,
                parsedRole,
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1))
            : null;
    }

    public void Upsert(WorkspaceMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        const string sql = """
            INSERT INTO workspace_memberships (workspace_id, user_id, role, display_name)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (workspace_id, user_id)
            DO UPDATE SET role = EXCLUDED.role, display_name = EXCLUDED.display_name
            """;
        using NpgsqlCommand command = database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(membership.WorkspaceId);
        command.Parameters.AddWithValue(membership.UserId);
        command.Parameters.AddWithValue(membership.Role.ToString());
        command.Parameters.AddWithValue(membership.DisplayName);
        command.ExecuteNonQuery();
    }
}
