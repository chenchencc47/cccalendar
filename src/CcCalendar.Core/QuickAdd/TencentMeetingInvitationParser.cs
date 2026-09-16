using System.Globalization;
using System.Text.RegularExpressions;

namespace CcCalendar.Core.QuickAdd;

/// <summary>
/// 腾讯会议邀请文本的解析结果。
/// </summary>
public sealed record TencentMeetingInvitation(
    string Title,
    string? MeetingNumber,
    string? JoinUrl,
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    string? Location);

/// <summary>
/// 解析腾讯会议复制的邀请内容：提取会议主题、会议号、入会链接、
/// 会议时间与与会地点，并支持把地点模糊匹配到已知会议室。
/// </summary>
public static partial class TencentMeetingInvitationParser
{
    [GeneratedRegex(@"(?:会议主题|会议名称)[:：]\s*(?<title>[^\r\n]+)")]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"#?腾讯会议[:：]\s*(?<number>[\d-]+)")]
    private static partial Regex MeetingNumberRegex();

    [GeneratedRegex(@"会议时间[:：]\s*(?<year>\d{4})\s*[/\-.年]\s*(?<month>\d{1,2})\s*[/\-.月]\s*(?<day>\d{1,2})\s*日?\s*(?:(?:\([^)]*\))|(?:（[^）]*）))?\s*(?<startHour>\d{1,2}):(?<startMinute>\d{2})\s*[-–~]\s*(?<endHour>\d{1,2}):(?<endMinute>\d{2})")]
    private static partial Regex MeetingTimeRegex();

    [GeneratedRegex(@"https://meeting\.tencent\.com/[A-Za-z0-9/_-]+")]
    private static partial Regex JoinUrlRegex();

    [GeneratedRegex(@"(?:与会地点|会议地点)[:：]\s*(?<location>[^\r\n]+)")]
    private static partial Regex LocationRegex();

    /// <summary>
    /// 尝试解析邀请文本；识别出会议主题与会议时间即视为成功，
    /// 会议号/链接/地点缺失时对应字段为 null。
    /// </summary>
    public static bool TryParse(string text, out TencentMeetingInvitation? invitation)
    {
        invitation = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match timeMatch = MeetingTimeRegex().Match(text);
        Match titleMatch = TitleRegex().Match(text);
        if (!timeMatch.Success || !titleMatch.Success)
        {
            return false;
        }

        var date = new DateOnly(
            int.Parse(timeMatch.Groups["year"].Value, CultureInfo.InvariantCulture),
            int.Parse(timeMatch.Groups["month"].Value, CultureInfo.InvariantCulture),
            int.Parse(timeMatch.Groups["day"].Value, CultureInfo.InvariantCulture));
        var start = new TimeOnly(
            int.Parse(timeMatch.Groups["startHour"].Value, CultureInfo.InvariantCulture),
            int.Parse(timeMatch.Groups["startMinute"].Value, CultureInfo.InvariantCulture));
        var end = new TimeOnly(
            int.Parse(timeMatch.Groups["endHour"].Value, CultureInfo.InvariantCulture),
            int.Parse(timeMatch.Groups["endMinute"].Value, CultureInfo.InvariantCulture));

        Match numberMatch = MeetingNumberRegex().Match(text);
        Match urlMatch = JoinUrlRegex().Match(text);
        Match locationMatch = LocationRegex().Match(text);

        invitation = new TencentMeetingInvitation(
            titleMatch.Groups["title"].Value.Trim(),
            numberMatch.Success ? numberMatch.Groups["number"].Value.Trim() : null,
            urlMatch.Success ? urlMatch.Value.Trim() : null,
            date,
            start,
            end,
            locationMatch.Success ? locationMatch.Groups["location"].Value.Trim() : null);
        return true;
    }

    /// <summary>
    /// 把邀请中的与会地点模糊匹配到已知会议室：候选房间名出现在地点文本中
    /// （或地点文本包含在房间名中）即命中；没有直接命中时，允许两者仅相差一个字符，
    /// 或房间名少一个字符的变体出现在带前后缀的地点文本中（如“佛山西樵财务三楼会议室”
    /// 匹配“财务部三楼会议室”），多个命中取最长的房间名。
    /// 无命中时返回第一个候选（约定为空串=不选会议室）。
    /// </summary>
    public static string MatchRoom(string? location, IReadOnlyList<string> rooms)
    {
        if (rooms.Count == 0)
        {
            return string.Empty;
        }

        string normalized = location?.Trim() ?? string.Empty;
        string? best = rooms
            .Where(room => !string.IsNullOrWhiteSpace(room)
                && (normalized.Contains(room, StringComparison.OrdinalIgnoreCase)
                    || room.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(room => room.Length)
            .FirstOrDefault();

        if (best is null && normalized.Length > 0)
        {
            best = rooms
                .Where(room => !string.IsNullOrWhiteSpace(room)
                    && IsSingleCharacterInsertionMatch(normalized, room))
                .OrderByDescending(room => room.Length)
                .FirstOrDefault();
        }

        if (best is null && normalized.Length > 0)
        {
            best = rooms
                .Where(room => !string.IsNullOrWhiteSpace(room)
                    && IsSingleCharacterVariantContained(normalized, room))
                .OrderByDescending(room => room.Length)
                .FirstOrDefault();
        }

        return best ?? rooms[0];
    }

    private static bool IsSingleCharacterInsertionMatch(string first, string second)
    {
        if (Math.Abs(first.Length - second.Length) != 1)
        {
            return false;
        }

        string shorter = first.Length < second.Length ? first : second;
        string longer = first.Length < second.Length ? second : first;
        for (int index = 0; index < longer.Length; index++)
        {
            if (string.Equals(longer.Remove(index, 1), shorter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 房间名少一个字符的变体出现在地点文本中即命中：
    /// 支持地点带城市/楼栋等前后缀且缺少房间名中的一个字符的场景。
    /// </summary>
    private static bool IsSingleCharacterVariantContained(string location, string room)
    {
        if (room.Length < 2)
        {
            return false;
        }

        for (int index = 0; index < room.Length; index++)
        {
            if (location.Contains(room.Remove(index, 1), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 四舍五入到最近的半小时（如 14:10 → 14:00、14:20 → 14:30），
    /// 供把任意会议时间对齐到半小时粒度的时间选项。
    /// </summary>
    public static TimeOnly RoundToNearestHalfHour(TimeOnly time)
    {
        int totalMinutes = time.Hour * 60 + time.Minute;
        int snapped = (int)Math.Round(totalMinutes / 30.0, MidpointRounding.AwayFromZero) * 30;
        if (snapped >= 24 * 60)
        {
            snapped = 23 * 60 + 30;
        }

        return new TimeOnly(snapped / 60, snapped % 60);
    }
}
