using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddClipboardHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClipboardHistoryEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                Preview = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                SearchText = table.Column<string>(type: "TEXT", nullable: false),
                TextContent = table.Column<string>(type: "TEXT", nullable: true),
                ImageRelativePath = table.Column<string>(type: "TEXT", nullable: true),
                FilePaths = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClipboardHistoryEntries", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClipboardHistoryEntries_CreatedAtUtc",
            table: "ClipboardHistoryEntries",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ClipboardHistoryEntries_IsFavorite",
            table: "ClipboardHistoryEntries",
            column: "IsFavorite");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ClipboardHistoryEntries");
    }
}
