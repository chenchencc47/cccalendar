using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

[Migration("20260823000200_AddEventMeetingReminder")]
public partial class AddEventMeetingReminder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ReminderLeadMinutes",
            table: "CalendarEvents",
            type: "INTEGER",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ReminderLeadMinutes", table: "CalendarEvents");
    }
}
