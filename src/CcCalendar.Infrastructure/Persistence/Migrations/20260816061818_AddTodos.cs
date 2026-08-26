using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTodos : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Todos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                DueAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                IsImportant = table.Column<bool>(type: "INTEGER", nullable: false),
                IsUrgentOverride = table.Column<bool>(type: "INTEGER", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Todos", x => x.Id);
                table.ForeignKey(
                    name: "FK_Todos_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "TodoSubtasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                TodoItemId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TodoSubtasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_TodoSubtasks_Todos_TodoItemId",
                    column: x => x.TodoItemId,
                    principalTable: "Todos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Todos_ProjectId",
            table: "Todos",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_TodoSubtasks_TodoItemId",
            table: "TodoSubtasks",
            column: "TodoItemId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TodoSubtasks");

        migrationBuilder.DropTable(
            name: "Todos");
    }
}
