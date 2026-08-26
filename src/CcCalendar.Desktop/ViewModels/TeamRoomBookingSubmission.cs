using CcCalendar.Core.QuickAdd;

namespace CcCalendar.Desktop.ViewModels;

public sealed record TeamRoomBookingSubmission(
    string Room,
    string Title,
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    string? MeetingInvitationText = null);

/// <summary>
/// 从快速新增请求解析可提交的团队会议室预约：
/// 仅处理带会议室地点的定时日程（全天/无地点/非日程返回 null）。
/// </summary>
public static class TeamRoomBookingSubmissionResolver
{
    public static TeamRoomBookingSubmission? TryCreate(
        QuickAddRequest request,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(timeZone);
        if (request.Kind != QuickAddKind.Event
            || string.IsNullOrWhiteSpace(request.Location)
            || request.StartAt is null
            || request.EndAt is null)
        {
            return null;
        }

        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(request.StartAt.Value, timeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(request.EndAt.Value, timeZone);
        if (localEnd <= localStart)
        {
            return null;
        }

        return new TeamRoomBookingSubmission(
            request.Location.Trim(),
            request.Title.Trim(),
            DateOnly.FromDateTime(localStart.DateTime),
            TimeOnly.FromDateTime(localStart.DateTime),
            TimeOnly.FromDateTime(localEnd.DateTime),
            request.MeetingInvitationText);
    }
}
