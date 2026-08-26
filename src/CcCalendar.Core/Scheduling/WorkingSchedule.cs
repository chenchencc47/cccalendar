namespace CcCalendar.Core.Scheduling;

public sealed class WorkingSchedule
{
    private readonly IReadOnlyDictionary<DayOfWeek, IReadOnlyList<(TimeOnly Start, TimeOnly End)>> periodsByDay;

    private WorkingSchedule(
        IReadOnlyDictionary<DayOfWeek, IReadOnlyList<(TimeOnly Start, TimeOnly End)>> periodsByDay)
    {
        this.periodsByDay = periodsByDay;
    }

    public static WorkingSchedule CreateDefault()
    {
        var periods = new Dictionary<DayOfWeek, IReadOnlyList<(TimeOnly Start, TimeOnly End)>>();

        foreach (DayOfWeek day in new[]
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
        })
        {
            periods[day] =
            [
                (new TimeOnly(8, 0), new TimeOnly(12, 0)),
                (new TimeOnly(14, 0), new TimeOnly(17, 30)),
            ];
        }

        return new WorkingSchedule(periods);
    }

    public IReadOnlyList<LocalTimeRange> GetWorkingRanges(DateOnly calendarDate)
    {
        if (!periodsByDay.TryGetValue(calendarDate.DayOfWeek, out var periods))
        {
            return [];
        }

        return periods
            .Select(period => new LocalTimeRange(calendarDate, period.Start, period.End))
            .ToArray();
    }

    public IReadOnlyList<LocalTimeRange> FindFreeRanges(
        DateOnly calendarDate,
        IEnumerable<LocalTimeRange> busyRanges)
    {
        ArgumentNullException.ThrowIfNull(busyRanges);

        LocalTimeRange[] busyForDate = [.. busyRanges
            .Where(range => range.CalendarDate == calendarDate)
            .OrderBy(range => range.StartTime)];
        var freeRanges = new List<LocalTimeRange>();

        foreach (LocalTimeRange workingRange in GetWorkingRanges(calendarDate))
        {
            TimeOnly cursor = workingRange.StartTime;

            foreach (LocalTimeRange busyRange in busyForDate)
            {
                if (busyRange.EndTime <= cursor || busyRange.StartTime >= workingRange.EndTime)
                {
                    continue;
                }

                TimeOnly busyStart = busyRange.StartTime < workingRange.StartTime
                    ? workingRange.StartTime
                    : busyRange.StartTime;

                if (busyStart > cursor)
                {
                    freeRanges.Add(new LocalTimeRange(calendarDate, cursor, busyStart));
                }

                if (busyRange.EndTime > cursor)
                {
                    cursor = busyRange.EndTime > workingRange.EndTime
                        ? workingRange.EndTime
                        : busyRange.EndTime;
                }
            }

            if (cursor < workingRange.EndTime)
            {
                freeRanges.Add(new LocalTimeRange(calendarDate, cursor, workingRange.EndTime));
            }
        }

        return freeRanges;
    }
}
