namespace CcCalendar.Core.Recurrence;

public interface IRecurrenceExpander
{
    IReadOnlyList<DateOnly> Expand(
        DateOnly seriesStart,
        RecurrenceRule rule,
        DateOnly rangeStart,
        DateOnly rangeEndExclusive,
        IReadOnlySet<DateOnly> excludedDates,
        IHolidayCalendar holidayCalendar);
}
