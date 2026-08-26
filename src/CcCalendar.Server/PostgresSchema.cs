namespace CcCalendar.Server;

public static class PostgresSchema
{
    public const string Definition = """
        CREATE EXTENSION IF NOT EXISTS btree_gist;

        CREATE TABLE IF NOT EXISTS rooms (
            id uuid PRIMARY KEY,
            workspace_id uuid NOT NULL,
            name text NOT NULL,
            time_zone_id text NOT NULL,
            is_active boolean NOT NULL DEFAULT true
        );
        CREATE INDEX IF NOT EXISTS ix_rooms_workspace ON rooms (workspace_id);

        CREATE TABLE IF NOT EXISTS room_bookings (
            id uuid PRIMARY KEY,
            workspace_id uuid NOT NULL,
            room_id uuid NOT NULL,
            organizer_id uuid NOT NULL,
            title text NOT NULL,
            start_at_utc timestamptz NOT NULL,
            end_at_utc timestamptz NOT NULL,
            time_zone_id text NOT NULL,
            meeting_invitation_text text NULL,
            idempotency_key text NOT NULL,
            FOREIGN KEY (room_id) REFERENCES rooms(id),
            CHECK (end_at_utc > start_at_utc),
            UNIQUE (workspace_id, idempotency_key),
            EXCLUDE USING gist (
                workspace_id WITH =,
                room_id WITH =,
                tstzrange(start_at_utc, end_at_utc, '[)') WITH &&
            )
        );
        CREATE INDEX IF NOT EXISTS ix_room_bookings_workspace ON room_bookings (workspace_id);
        ALTER TABLE room_bookings ADD COLUMN IF NOT EXISTS meeting_invitation_text text NULL;

        CREATE TABLE IF NOT EXISTS workspace_memberships (
            workspace_id uuid NOT NULL,
            user_id uuid NOT NULL,
            role text NOT NULL,
            display_name text NOT NULL DEFAULT '',
            PRIMARY KEY (workspace_id, user_id)
        );
        ALTER TABLE workspace_memberships ADD COLUMN IF NOT EXISTS display_name text NOT NULL DEFAULT '';

        CREATE TABLE IF NOT EXISTS workspace_sync_versions (
            workspace_id uuid PRIMARY KEY,
            version bigint NOT NULL
        );
        CREATE TABLE IF NOT EXISTS sync_changes (
            workspace_id uuid NOT NULL,
            version bigint NOT NULL,
            entity_type text NOT NULL,
            entity_id uuid NOT NULL,
            operation text NOT NULL,
            PRIMARY KEY (workspace_id, version),
            UNIQUE (workspace_id, version)
        );
        """;
}
