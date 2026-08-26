using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTodoPlanning : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "StartAtUtc",
            table: "Todos",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "TodoDependencies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DependsOnTodoItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                TodoItemId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TodoDependencies", x => x.Id);
                table.ForeignKey(
                    name: "FK_TodoDependencies_Todos_DependsOnTodoItemId",
                    column: x => x.DependsOnTodoItemId,
                    principalTable: "Todos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TodoDependencies_Todos_TodoItemId",
                    column: x => x.TodoItemId,
                    principalTable: "Todos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TodoDependencies_DependsOnTodoItemId",
            table: "TodoDependencies",
            column: "DependsOnTodoItemId");

        migrationBuilder.CreateIndex(
            name: "IX_TodoDependencies_TodoItemId_DependsOnTodoItemId",
            table: "TodoDependencies",
            columns: new[] { "TodoItemId", "DependsOnTodoItemId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TodoDependencies");

        migrationBuilder.DropColumn(
            name: "StartAtUtc",
            table: "Todos");
    }
}
