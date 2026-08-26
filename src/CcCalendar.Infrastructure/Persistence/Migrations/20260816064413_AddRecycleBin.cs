using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRecycleBin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "WorkRecords",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "Todos",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "Projects",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "CalendarEvents",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DeletedAtUtc",
            table: "WorkRecords");

        migrationBuilder.DropColumn(
            name: "DeletedAtUtc",
            table: "Todos");

        migrationBuilder.DropColumn(
            name: "DeletedAtUtc",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "DeletedAtUtc",
            table: "CalendarEvents");
    }
}
