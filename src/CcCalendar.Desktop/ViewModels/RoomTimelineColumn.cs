namespace CcCalendar.Desktop.ViewModels;

public sealed record RoomTimelineColumn(
    string Room,
    IReadOnlyList<TimelineBookingBar> Bars)
{
    public string BookingCountText => Bars.Count == 0 ? "空闲" : $"{Bars.Count} 项预订";
}
