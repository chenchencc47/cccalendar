using System.Globalization;
using CcCalendar.Core.QuickAdd;

namespace CcCalendar.Core.Tests.QuickAdd;

public sealed class TencentMeetingInvitationParserTests
{
    private const string SampleInvitation = """
        周家丞 邀请您参加腾讯会议
        会议主题：数字化平台重构项目每周例会
        会议时间：2026/08/27 14:10-15:10 (GMT+08:00) 中国标准时间 - 北京
        重复周期：2026/08/06-2027/02/25 14:10-15:10, 每周 (周四)

        点击链接入会，或添加至会议列表：
        https://meeting.tencent.com/dm/AzRnftL11yo6

        #腾讯会议：749-5361-1348

        手机一键拨号入会
        +8675536550000,74953611348 (中国大陆)

        根据您的位置拨号
        +86 (0)755 36550000 (中国大陆)

        与会地点：佛山西樵研发中心三楼会议室

        复制该信息，打开手机腾讯会议即可参与
        """;

    [Fact]
    public void ParsesFullInvitation()
    {
        bool parsed = TencentMeetingInvitationParser.TryParse(SampleInvitation, out TencentMeetingInvitation? invitation);

        Assert.True(parsed);
        Assert.NotNull(invitation);
        Assert.Equal("数字化平台重构项目每周例会", invitation!.Title);
        Assert.Equal("749-5361-1348", invitation.MeetingNumber);
        Assert.Equal("https://meeting.tencent.com/dm/AzRnftL11yo6", invitation.JoinUrl);
        Assert.Equal(DateOnly.Parse("2026-08-27", CultureInfo.InvariantCulture), invitation.Date);
        Assert.Equal(new TimeOnly(14, 10), invitation.Start);
        Assert.Equal(new TimeOnly(15, 10), invitation.End);
        Assert.Equal("佛山西樵研发中心三楼会议室", invitation.Location);
    }

    [Fact]
    public void ParsesInvitationWithoutLocation()
    {
        const string text = """
            会议主题：站会
            会议时间：2026/08/28 09:00-09:30 (GMT+08:00)
            #腾讯会议：111-2222-333
            """;

        bool parsed = TencentMeetingInvitationParser.TryParse(text, out TencentMeetingInvitation? invitation);

        Assert.True(parsed);
        Assert.NotNull(invitation);
        Assert.Equal("站会", invitation!.Title);
        Assert.Equal("111-2222-333", invitation.MeetingNumber);
        Assert.Null(invitation.Location);
    }

    [Fact]
    public void ParsesChineseDateNameLocationAndLinkFragmentFormat()
    {
        const string text = """
            会议名称：销售排产订单管理流程蓝图沟通会议
            会议时间：2026年8月24日（星期一）14:30-17:00
            会议地点：集团聚英堂会议室
            点击链接入会，或添加至会议列表：https://meeting.tencent.com/dm/fYF62CD5t2LO#腾讯会议：242-471-769复制该信息，打开手机腾讯会议即可参与
            """;

        bool parsed = TencentMeetingInvitationParser.TryParse(text, out TencentMeetingInvitation? invitation);

        Assert.True(parsed);
        Assert.NotNull(invitation);
        Assert.Equal("销售排产订单管理流程蓝图沟通会议", invitation!.Title);
        Assert.Equal("242-471-769", invitation.MeetingNumber);
        Assert.Equal("https://meeting.tencent.com/dm/fYF62CD5t2LO", invitation.JoinUrl);
        Assert.Equal(new DateOnly(2026, 8, 24), invitation.Date);
        Assert.Equal(new TimeOnly(14, 30), invitation.Start);
        Assert.Equal(new TimeOnly(17, 0), invitation.End);
        Assert.Equal("集团聚英堂会议室", invitation.Location);
    }

    [Fact]
    public void RejectsTextWithoutMeetingTime()
    {
        bool parsed = TencentMeetingInvitationParser.TryParse("今天下午开会聊聊", out _);

        Assert.False(parsed);
    }

    [Fact]
    public void MatchRoomPrefersRoomContainedInLocation()
    {
        string matched = TencentMeetingInvitationParser.MatchRoom(
            "佛山西樵研发中心三楼会议室",
            ["", "一号会议室", "研发中心三楼会议室", "云会议室A"]);

        Assert.Equal("研发中心三楼会议室", matched);
    }

    [Fact]
    public void MatchRoomPrefersLongestCandidateWhenMultipleMatch()
    {
        string matched = TencentMeetingInvitationParser.MatchRoom(
            "佛山西樵研发中心三楼会议室A区",
            ["研发中心", "研发中心三楼会议室A区", "会议室A区"]);

        Assert.Equal("研发中心三楼会议室A区", matched);
    }

    [Fact]
    public void MatchRoomReturnsFirstWhenNothingMatches()
    {
        string matched = TencentMeetingInvitationParser.MatchRoom(
            "线上会议",
            ["", "一号会议室", "研发中心三楼会议室"]);

        Assert.Equal(string.Empty, matched);
    }

    [Fact]
    public void RoundToNearestHalfHourSnapsTimes()
    {
        Assert.Equal(new TimeOnly(14, 0), TencentMeetingInvitationParser.RoundToNearestHalfHour(new TimeOnly(14, 10)));
        Assert.Equal(new TimeOnly(15, 0), TencentMeetingInvitationParser.RoundToNearestHalfHour(new TimeOnly(15, 10)));
        Assert.Equal(new TimeOnly(9, 30), TencentMeetingInvitationParser.RoundToNearestHalfHour(new TimeOnly(9, 20)));
        Assert.Equal(new TimeOnly(23, 30), TencentMeetingInvitationParser.RoundToNearestHalfHour(new TimeOnly(23, 50)));
    }
}
