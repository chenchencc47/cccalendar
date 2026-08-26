using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CcCalendar.Infrastructure.Persistence.Migrations;

[Migration("20260823000100_AddMeetingInvitation")]
public partial class AddMeetingInvitation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MeetingInvitationText",
            table: "CalendarEvents",
            type: "TEXT",
            maxLength: 20000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MeetingInvitationExpiresOn",
            table: "CalendarEvents",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MeetingInvitationExpiresOn", table: "CalendarEvents");
        migrationBuilder.DropColumn(name: "MeetingInvitationText", table: "CalendarEvents");
    }
}
