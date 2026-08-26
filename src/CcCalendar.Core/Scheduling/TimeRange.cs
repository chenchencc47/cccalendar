namespace CcCalendar.Core.Scheduling;

public readonly record struct TimeRange
{
    public TimeRange(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        StartAtUtc = startAt.ToUniversalTime();
        EndAtUtc = endAt.ToUniversalTime();

        if (EndAtUtc <= StartAtUtc)
        {
            throw new ArgumentException("The end time must be after the start time.", nameof(endAt));
        }
    }

    public DateTimeOffset StartAtUtc { get; }

    public DateTimeOffset EndAtUtc { get; }
}
