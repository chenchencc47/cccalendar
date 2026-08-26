using CcCalendar.Core.QuickAdd;

namespace CcCalendar.Desktop.ViewModels;

public sealed record EventEditState(
    DateOnly SelectedDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsAllDay,
    DateOnly? AllDayStart,
    DateOnly? AllDayEndExclusive)
{
    public ScheduleUpdateRequest CreateUpdateRequest(
        Guid eventId,
        string title,
        string? location,
        TimeZoneInfo timeZone,
        int? reminderLeadMinutes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(timeZone);

        if (IsAllDay)
        {
            if (AllDayStart is null || AllDayEndExclusive is null)
            {
                throw new ArgumentException("An all-day schedule requires the date range.");
            }

            return new ScheduleUpdateRequest(
                eventId,
                title.Trim(),
                location,
                IsAllDay: true,
                null,
                null,
                null,
                AllDayStart,
                AllDayEndExclusive,
                null);
        }

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
        return new ScheduleUpdateRequest(
            eventId,
            title.Trim(),
            location,
            IsAllDay: false,
            new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart)),
            new DateTimeOffset(localEnd, timeZone.GetUtcOffset(localEnd)),
            timeZone.Id,
            null,
            null,
            reminderLeadMinutes);
    }
}
