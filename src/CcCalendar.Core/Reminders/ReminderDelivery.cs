namespace CcCalendar.Core.Reminders;

public sealed class ReminderDelivery
{
    private ReminderDelivery()
    {
        Title = string.Empty;
    }

    private ReminderDelivery(
        Reminder reminder,
        DateTimeOffset dispatchedAtUtc,
        bool wasMissed)
    {
        Id = Guid.NewGuid();
        ReminderId = reminder.Id;
        TargetType = reminder.TargetType;
        TargetId = reminder.TargetId;
        Title = reminder.Title;
        TriggerAtUtc = reminder.TriggerAtUtc;
        DispatchedAtUtc = dispatchedAtUtc;
        WasMissed = wasMissed;
    }

    public Guid Id { get; private set; }

    public Guid ReminderId { get; private set; }

    public ReminderTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public string Title { get; private set; }

    public DateTimeOffset TriggerAtUtc { get; private set; }

    public DateTimeOffset DispatchedAtUtc { get; private set; }

    public bool WasMissed { get; private set; }

    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }

    public DateTimeOffset? SnoozedUntilUtc { get; private set; }

    internal static ReminderDelivery Create(
        Reminder reminder,
        DateTimeOffset dispatchedAtUtc,
        bool wasMissed)
    {
        return new ReminderDelivery(reminder, dispatchedAtUtc, wasMissed);
    }

    public void Acknowledge(DateTimeOffset acknowledgedAtUtc)
    {
        if (!AcknowledgedAtUtc.HasValue)
        {
            AcknowledgedAtUtc = acknowledgedAtUtc.ToUniversalTime();
            SnoozedUntilUtc = null;
        }
    }

    public void Snooze(DateTimeOffset snoozedAtUtc, TimeSpan duration)
    {
        if (AcknowledgedAtUtc.HasValue)
        {
            throw new InvalidOperationException("An acknowledged reminder cannot be snoozed.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        SnoozedUntilUtc = snoozedAtUtc.ToUniversalTime().Add(duration);
    }
}
