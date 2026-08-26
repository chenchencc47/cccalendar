using CcCalendar.Core.QuickAdd;

namespace CcCalendar.Desktop.ViewModels;

public sealed record ScheduleEditorState(
    DateOnly SelectedDate,
    TimeOnly StartTime,
    TimeOnly EndTime)
{
    public QuickAddRequest CreateRequest(string title, TimeZoneInfo timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(timeZone);
        if (EndTime <= StartTime)
        {
            throw new ArgumentException("The end time must be after the start time.");
        }

        DateTime localStart = DateTime.SpecifyKind(
            SelectedDate.ToDateTime(StartTime),
            DateTimeKind.Unspecified);
        DateTime localEnd = DateTime.SpecifyKind(
            SelectedDate.ToDateTime(EndTime),
            DateTimeKind.Unspecified);
        return new QuickAddRequest(
            QuickAddKind.Event,
            title.Trim(),
            new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart)),
            new DateTimeOffset(localEnd, timeZone.GetUtcOffset(localEnd)),
            timeZone.Id);
    }

    public QuickAddRequest CreateAllDayRequest(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new QuickAddRequest(
            QuickAddKind.Event,
            title.Trim(),
            null,
            null,
            null,
            SelectedDate,
            SelectedDate.AddDays(1));
    }
}
