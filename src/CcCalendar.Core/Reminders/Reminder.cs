namespace CcCalendar.Core.Reminders;

public sealed class Reminder
{
    private Reminder()
    {
        Title = string.Empty;
    }

    private Reminder(
        ReminderTargetType targetType,
        Guid targetId,
        string title,
        DateTimeOffset triggerAtUtc,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        TargetType = targetType;
        TargetId = targetId;
        Title = title;
        TriggerAtUtc = triggerAtUtc.ToUniversalTime();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public ReminderTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public string Title { get; private set; }

    public DateTimeOffset TriggerAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? QueuedAtUtc { get; private set; }

    public static Reminder Create(
        ReminderTargetType targetType,
        Guid targetId,
        string title,
        DateTimeOffset triggerAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("A reminder target identifier is required.", nameof(targetId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Reminder(
            targetType,
            targetId,
            title.Trim(),
            triggerAtUtc,
            createdAtUtc);
    }

    public ReminderDelivery Queue(
        DateTimeOffset dispatchedAtUtc,
        TimeSpan missedGracePeriod)
    {
        if (QueuedAtUtc.HasValue)
        {
            throw new InvalidOperationException("The reminder has already been queued.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(missedGracePeriod, TimeSpan.Zero);

        DateTimeOffset normalizedDispatch = dispatchedAtUtc.ToUniversalTime();
        if (normalizedDispatch < TriggerAtUtc)
        {
            throw new InvalidOperationException("A reminder cannot be queued before its trigger time.");
        }

        QueuedAtUtc = normalizedDispatch;
        return ReminderDelivery.Create(
            this,
            normalizedDispatch,
            TriggerAtUtc < normalizedDispatch - missedGracePeriod);
    }
}
