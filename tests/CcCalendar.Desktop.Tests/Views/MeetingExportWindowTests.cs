using CcCalendar.Core.Schedules;

namespace CcCalendar.Desktop.Tests.Views;

public sealed class MeetingExportWindowTests
{
    [Fact]
    public void NumberAndLinkContainsOnlyTencentMeetingAccessInformation()
    {
        CalendarEvent meeting = CalendarEvent.CreateTimed(
            "项目周会",
            null,
            new DateTimeOffset(2026, 8, 27, 14, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 27, 15, 0, 0, TimeSpan.FromHours(8)),
            "China Standard Time",
            "研发中心三楼会议室",
            """
            会议主题：项目周会
            会议时间：2026/08/27 14:00-15:00
            https://meeting.tencent.com/dm/example
            #腾讯会议：749-5361-1348
            """);

        string text = MeetingExportWindow.NumberAndLink(meeting);

        Assert.Contains("会议号：749-5361-1348", text);
        Assert.Contains("https://meeting.tencent.com/dm/example", text);
        Assert.DoesNotContain("会议主题", text);
    }
}
