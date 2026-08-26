using System.Net;
using System.Net.Http;
using CcCalendar.Desktop;

namespace CcCalendar.Desktop.Tests;

public sealed class MainWindowBookingErrorTests
{
    [Fact]
    public void ConflictMessageExplainsThatLocalScheduleWasSaved()
    {
        string message = MainWindow.FormatTeamBookingFailure(
            new HttpRequestException("{\"code\":\"room_booking_conflict\"}", null, HttpStatusCode.Conflict));

        Assert.Contains("该会议室在此时间段已被预约", message);
        Assert.Contains("本地日程已保存", message);
        Assert.DoesNotContain("room_booking_conflict", message);
    }

    [Fact]
    public void NonConflictMessageKeepsUsefulServerDetail()
    {
        string message = MainWindow.FormatTeamBookingFailure(
            new HttpRequestException("服务器暂时不可用", null, HttpStatusCode.ServiceUnavailable));

        Assert.Contains("服务器暂时不可用", message);
    }

    [Fact]
    public void UnavailableMessageExplainsIdentityRefresh()
    {
        string message = MainWindow.FormatTeamBookingUnavailable("项目组二楼会议室");

        Assert.Contains("重新登录", message);
        Assert.Contains("项目组二楼会议室", message);
    }
}
