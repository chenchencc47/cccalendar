namespace CcCalendar.Core.Scheduling;

public readonly record struct LocalTimeRange
{
    public LocalTimeRange(DateOnly calendarDate, TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentException("The end time must be after the start time.", nameof(endTime));
        }

        CalendarDate = calendarDate;
        StartTime = startTime;
        EndTime = endTime;
    }

    public DateOnly CalendarDate { get; }

    public TimeOnly StartTime { get; }

    public TimeOnly EndTime { get; }
}
