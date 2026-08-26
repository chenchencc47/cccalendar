using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSyncMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SyncMetadata",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                PullCursor = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SyncMetadata", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SyncOutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                Operation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                LastAttemptAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                AcknowledgedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SyncOutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SyncMetadata_DeviceId",
            table: "SyncMetadata",
            column: "DeviceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SyncOutboxMessages_AcknowledgedAtUtc_CreatedAtUtc",
            table: "SyncOutboxMessages",
            columns: new[] { "AcknowledgedAtUtc", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SyncOutboxMessages_EntityType_EntityId",
            table: "SyncOutboxMessages",
            columns: new[] { "EntityType", "EntityId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SyncMetadata");

        migrationBuilder.DropTable(
            name: "SyncOutboxMessages");
    }
}
