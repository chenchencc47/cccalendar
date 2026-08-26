using CcCalendar.Core.Schedules;

namespace CcCalendar.Desktop.ViewModels;

public sealed record TimelineBookingBar(
    Guid EventId,
    string Title,
    RoomBookingGeometry Geometry,
    bool IsEditing);

/// <summary>
/// 编辑窗右侧时间条的预览数据：按会议室筛选当日定时日程，
/// 正在编辑的日程以高亮标识。
/// </summary>
public static class EventTimelinePreviewBuilder
{
    public static IReadOnlyList<TimelineBookingBar> Build(
        IReadOnlyList<CalendarEvent> dayEvents,
        Guid editingEventId,
        string? room,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(dayEvents);
        ArgumentNullException.ThrowIfNull(timeZone);

        return [.. dayEvents
            .Where(calendarEvent => !calendarEvent.IsAllDay)
            .Where(calendarEvent => room is null
                || string.Equals(
                    calendarEvent.Location,
                    room,
                    StringComparison.OrdinalIgnoreCase))
            .Select(calendarEvent => new TimelineBookingBar(
                calendarEvent.Id,
                calendarEvent.Title,
                RoomTimelineLayout.GetGeometry(
                    calendarEvent.StartAtUtc!.Value,
                    calendarEvent.EndAtUtc!.Value - calendarEvent.StartAtUtc!.Value,
                    timeZone),
                calendarEvent.Id == editingEventId))
            .Where(bar => bar.Geometry.Height > 0)
            .OrderBy(bar => bar.Geometry.Top)];
    }
}
