using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAuditEntries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                CanUndo = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEntries", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_EntityType_EntityId_OccurredAtUtc",
            table: "AuditEntries",
            columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_OccurredAtUtc",
            table: "AuditEntries",
            column: "OccurredAtUtc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditEntries");
    }
}
