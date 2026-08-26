using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReminders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Reminders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                TriggerAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                QueuedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reminders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ReminderDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ReminderId = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                TriggerAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                DispatchedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                WasMissed = table.Column<bool>(type: "INTEGER", nullable: false),
                AcknowledgedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReminderDeliveries", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReminderDeliveries_Reminders_ReminderId",
                    column: x => x.ReminderId,
                    principalTable: "Reminders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReminderDeliveries_AcknowledgedAtUtc_DispatchedAtUtc",
            table: "ReminderDeliveries",
            columns: new[] { "AcknowledgedAtUtc", "DispatchedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ReminderDeliveries_ReminderId",
            table: "ReminderDeliveries",
            column: "ReminderId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Reminders_QueuedAtUtc_TriggerAtUtc",
            table: "Reminders",
            columns: new[] { "QueuedAtUtc", "TriggerAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Reminders_TargetType_TargetId_TriggerAtUtc",
            table: "Reminders",
            columns: new[] { "TargetType", "TargetId", "TriggerAtUtc" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ReminderDeliveries");

        migrationBuilder.DropTable(
            name: "Reminders");
    }
}
