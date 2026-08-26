using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WorkRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkRecords_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "RecordAttachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                MediaType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                WorkRecordId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecordAttachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecordAttachments_WorkRecords_WorkRecordId",
                    column: x => x.WorkRecordId,
                    principalTable: "WorkRecords",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RecordVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                SavedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                WorkRecordId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecordVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecordVersions_WorkRecords_WorkRecordId",
                    column: x => x.WorkRecordId,
                    principalTable: "WorkRecords",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RecordAttachments_WorkRecordId",
            table: "RecordAttachments",
            column: "WorkRecordId");

        migrationBuilder.CreateIndex(
            name: "IX_RecordVersions_WorkRecordId_VersionNumber",
            table: "RecordVersions",
            columns: new[] { "WorkRecordId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WorkRecords_ProjectId",
            table: "WorkRecords",
            column: "ProjectId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RecordAttachments");

        migrationBuilder.DropTable(
            name: "RecordVersions");

        migrationBuilder.DropTable(
            name: "WorkRecords");
    }
}
