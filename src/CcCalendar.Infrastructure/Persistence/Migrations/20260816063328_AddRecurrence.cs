using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRecurrence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RecurrenceSeries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CalendarEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                Frequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Interval = table.Column<int>(type: "INTEGER", nullable: false),
                WeekdayMask = table.Column<int>(type: "INTEGER", nullable: false),
                UntilInclusive = table.Column<DateOnly>(type: "TEXT", nullable: true),
                SkipHolidays = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecurrenceSeries", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecurrenceSeries_CalendarEvents_CalendarEventId",
                    column: x => x.CalendarEventId,
                    principalTable: "CalendarEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ExcludedOccurrences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurrenceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                RecurrenceSeriesId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExcludedOccurrences", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExcludedOccurrences_RecurrenceSeries_RecurrenceSeriesId",
                    column: x => x.RecurrenceSeriesId,
                    principalTable: "RecurrenceSeries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExcludedOccurrences_RecurrenceSeriesId_OccurrenceDate",
            table: "ExcludedOccurrences",
            columns: new[] { "RecurrenceSeriesId", "OccurrenceDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RecurrenceSeries_CalendarEventId",
            table: "RecurrenceSeries",
            column: "CalendarEventId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExcludedOccurrences");

        migrationBuilder.DropTable(
            name: "RecurrenceSeries");
    }
}
