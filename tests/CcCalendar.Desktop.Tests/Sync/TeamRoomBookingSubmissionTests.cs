using CcCalendar.Core.QuickAdd;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.Sync;

public sealed class TeamRoomBookingSubmissionTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    [Fact]
    public void TryCreateResolvesRoomTitleDateAndLocalTimes()
    {
        // 2026-08-24 01:00Z = 09:00 +08:00。
        var request = new QuickAddRequest(
            QuickAddKind.Event,
            "周会",
            new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 2, 30, 0, TimeSpan.Zero),
            "UTC",
            Location: "云会议室A");

        TeamRoomBookingSubmission? submission = TeamRoomBookingSubmissionResolver.TryCreate(request, Zone);

        Assert.NotNull(submission);
        Assert.Equal("云会议室A", submission!.Room);
        Assert.Equal("周会", submission.Title);
        Assert.Equal(new DateOnly(2026, 8, 24), submission.Date);
        Assert.Equal(new TimeOnly(9, 0), submission.Start);
        Assert.Equal(new TimeOnly(10, 30), submission.End);
    }

    [Fact]
    public void TryCreateReturnsNullForNonEventAllDayOrMissingRoomOrTimes()
    {
        var timedWithRoom = new QuickAddRequest(
            QuickAddKind.Event,
            "周会",
            new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
            "UTC",
            Location: "云会议室A");

        Assert.Null(TeamRoomBookingSubmissionResolver.TryCreate(
            timedWithRoom with { Kind = QuickAddKind.Todo },
            Zone));
        Assert.Null(TeamRoomBookingSubmissionResolver.TryCreate(
            timedWithRoom with { Location = null },
            Zone));
        Assert.Null(TeamRoomBookingSubmissionResolver.TryCreate(
            timedWithRoom with { StartAt = null, EndAt = null, AllDayStart = new DateOnly(2026, 8, 24), AllDayEndExclusive = new DateOnly(2026, 8, 25) },
            Zone));
        Assert.Null(TeamRoomBookingSubmissionResolver.TryCreate(
            timedWithRoom with { Location = "   " },
            Zone));
    }

    [Fact]
    public void TryCreateCarriesInvitationTextForRemoteDetails()
    {
        const string invitation = "会议主题：每日例会\n#腾讯会议：365-5683-5623";
        var request = new QuickAddRequest(
            QuickAddKind.Event,
            "每日例会",
            new DateTimeOffset(2026, 8, 24, 10, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 11, 10, 0, TimeSpan.Zero),
            "UTC",
            Location: "项目组二楼会议室",
            MeetingInvitationText: invitation);

        Assert.Equal(invitation, TeamRoomBookingSubmissionResolver.TryCreate(request, Zone)!.MeetingInvitationText);
    }
}
