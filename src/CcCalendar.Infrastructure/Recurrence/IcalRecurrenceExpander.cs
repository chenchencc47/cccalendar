using CcCalendar.Core.Recurrence;
using Ical.Net;
using Ical.Net.DataTypes;
using CalendarRecurrenceRule = CcCalendar.Core.Recurrence.RecurrenceRule;
using IcalCalendarEvent = Ical.Net.CalendarComponents.CalendarEvent;
using IcalRecurrencePattern = Ical.Net.DataTypes.RecurrencePattern;

namespace CcCalendar.Infrastructure.Recurrence;

public sealed class IcalRecurrenceExpander : IRecurrenceExpander
{
    public IReadOnlyList<DateOnly> Expand(
        DateOnly seriesStart,
        CalendarRecurrenceRule rule,
        DateOnly rangeStart,
        DateOnly rangeEndExclusive,
        IReadOnlySet<DateOnly> excludedDates,
        IHolidayCalendar holidayCalendar)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(excludedDates);
        ArgumentNullException.ThrowIfNull(holidayCalendar);

        if (rangeEndExclusive <= rangeStart)
        {
            throw new ArgumentException("The range end must be after the range start.", nameof(rangeEndExclusive));
        }

        IcalRecurrencePattern pattern = CreatePattern(rule);
        var calendarEvent = new IcalCalendarEvent
        {
            DtStart = new CalDateTime(seriesStart),
            RecurrenceRule = pattern,
        };

        return calendarEvent
            .GetOccurrences(new CalDateTime(rangeStart))
            .TakeWhile(occurrence => occurrence.Period.StartTime.Date < rangeEndExclusive)
            .Select(occurrence => occurrence.Period.StartTime.Date)
            .Where(calendarDate => !excludedDates.Contains(calendarDate))
            .Where(calendarDate => !rule.SkipHolidays || !holidayCalendar.IsHoliday(calendarDate))
            .ToArray();
    }

    private static IcalRecurrencePattern CreatePattern(CalendarRecurrenceRule rule)
    {
        var pattern = new IcalRecurrencePattern
        {
            Frequency = rule.Frequency switch
            {
                RecurrenceFrequency.Daily => FrequencyType.Daily,
                RecurrenceFrequency.Weekly => FrequencyType.Weekly,
                RecurrenceFrequency.Monthly => FrequencyType.Monthly,
                RecurrenceFrequency.Yearly => FrequencyType.Yearly,
                _ => throw new ArgumentOutOfRangeException(nameof(rule)),
            },
            Interval = rule.Interval,
        };

        if (rule.DaysOfWeek.Count > 0)
        {
            pattern.ByDay = [.. rule.DaysOfWeek.Select(day => new WeekDay(day))];
        }

        if (rule.UntilInclusive.HasValue)
        {
            pattern.Until = new CalDateTime(rule.UntilInclusive.Value);
        }

        return pattern;
    }
}
