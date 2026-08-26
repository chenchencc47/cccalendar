namespace CcCalendar.Desktop.ViewModels;

public readonly record struct RoomBookingGeometry(double Top, double Height);

/// <summary>
/// 会议室视图和编辑窗时间条共用的垂直时间轴几何：
/// 08:00–22:00，半小时一行，每行 26 像素。
/// </summary>
public static class RoomTimelineLayout
{
    public const int StartHour = 8;
    public const int EndHour = 22;
    public const double RowHeight = 26;
    public const int TotalRows = (EndHour - StartHour) * 2;
    public const double TotalHeight = TotalRows * RowHeight;

    public static RoomBookingGeometry GetGeometry(
        DateTimeOffset startUtc,
        TimeSpan duration,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(startUtc, timeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(startUtc + duration, timeZone);
        double startOffset = localStart.Hour * 60 + localStart.Minute - StartHour * 60;
        double endOffset = localEnd.Hour * 60 + localEnd.Minute - StartHour * 60
            + (localEnd.Date > localStart.Date ? 24 * 60 : 0);
        double windowMinutes = (EndHour - StartHour) * 60;

        double clampedStart = Math.Clamp(startOffset, 0, windowMinutes);
        double clampedEnd = Math.Clamp(endOffset, 0, windowMinutes);
        return new RoomBookingGeometry(
            clampedStart / 60 * 2 * RowHeight,
            Math.Max(0, clampedEnd - clampedStart) / 60 * 2 * RowHeight);
    }
}
