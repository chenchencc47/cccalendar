namespace CcCalendar.Desktop.ViewModels;

public sealed record CalendarEventDetailViewModel(
    Guid Id,
    string Title,
    string TimeText,
    bool IsAllDay,
    string? Location = null)
{
    public string LocationText => string.IsNullOrWhiteSpace(Location)
        ? "Collapsed"
        : "Visible";
}
