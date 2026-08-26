using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSchedules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CalendarEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                StartAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                EndAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                AllDayStart = table.Column<DateOnly>(type: "TEXT", nullable: true),
                AllDayEndExclusive = table.Column<DateOnly>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_CalendarEvents_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "TimeBlocks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TodoItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                StartAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                EndAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TimeBlocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_TimeBlocks_Todos_TodoItemId",
                    column: x => x.TodoItemId,
                    principalTable: "Todos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEvents_ProjectId",
            table: "CalendarEvents",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_TimeBlocks_TodoItemId",
            table: "TimeBlocks",
            column: "TodoItemId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CalendarEvents");

        migrationBuilder.DropTable(
            name: "TimeBlocks");
    }
}
