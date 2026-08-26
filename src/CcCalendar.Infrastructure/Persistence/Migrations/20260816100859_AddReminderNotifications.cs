using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReminderNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReminderDeliveries_AcknowledgedAtUtc_DispatchedAtUtc",
            table: "ReminderDeliveries");

        migrationBuilder.AddColumn<long>(
            name: "SnoozedUntilUtc",
            table: "ReminderDeliveries",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReminderDeliveries_AcknowledgedAtUtc_SnoozedUntilUtc_DispatchedAtUtc",
            table: "ReminderDeliveries",
            columns: new[] { "AcknowledgedAtUtc", "SnoozedUntilUtc", "DispatchedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReminderDeliveries_AcknowledgedAtUtc_SnoozedUntilUtc_DispatchedAtUtc",
            table: "ReminderDeliveries");

        migrationBuilder.DropColumn(
            name: "SnoozedUntilUtc",
            table: "ReminderDeliveries");

        migrationBuilder.CreateIndex(
            name: "IX_ReminderDeliveries_AcknowledgedAtUtc_DispatchedAtUtc",
            table: "ReminderDeliveries",
            columns: new[] { "AcknowledgedAtUtc", "DispatchedAtUtc" });
    }
}
