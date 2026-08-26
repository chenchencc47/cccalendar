namespace CcCalendar.Core.Schedules;

public sealed class TimeBlock
{
    private TimeBlock()
    {
        TimeZoneId = null!;
    }

    public Guid Id { get; private set; }

    public Guid TodoItemId { get; private set; }

    public DateTimeOffset StartAtUtc { get; private set; }

    public DateTimeOffset EndAtUtc { get; private set; }

    public string TimeZoneId { get; private set; }

    public TimeSpan Duration => EndAtUtc - StartAtUtc;

    public static TimeBlock Create(
        Guid todoItemId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        if (todoItemId == Guid.Empty)
        {
            throw new ArgumentException("A time block must reference a todo item.", nameof(todoItemId));
        }

        DateTimeOffset startAtUtc = startAt.ToUniversalTime();
        DateTimeOffset endAtUtc = endAt.ToUniversalTime();

        if (endAtUtc <= startAtUtc)
        {
            throw new ArgumentException("The end time must be after the start time.", nameof(endAt));
        }

        return new TimeBlock
        {
            Id = Guid.NewGuid(),
            TodoItemId = todoItemId,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            TimeZoneId = timeZoneId.Trim(),
        };
    }
}
