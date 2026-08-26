using CcCalendar.Core.Reminders;

namespace CcCalendar.Core.Tests.Reminders;

public sealed class ReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void QueueCreatesMissedDeliveryAndPreventsDuplicateQueueing()
    {
        Reminder reminder = Reminder.Create(
            ReminderTargetType.CalendarEvent,
            Guid.NewGuid(),
            "项目评审",
            Now.AddMinutes(-20),
            Now.AddHours(-1));

        ReminderDelivery delivery = reminder.Queue(Now, TimeSpan.FromMinutes(1));

        Assert.Equal(reminder.Id, delivery.ReminderId);
        Assert.Equal("项目评审", delivery.Title);
        Assert.True(delivery.WasMissed);
        Assert.Equal(Now, reminder.QueuedAtUtc);
        Assert.Throws<InvalidOperationException>(
            () => reminder.Queue(Now.AddSeconds(1), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void QueueWithinGracePeriodIsNotMarkedMissed()
    {
        Reminder reminder = Reminder.Create(
            ReminderTargetType.Todo,
            Guid.NewGuid(),
            "提交周报",
            Now.AddSeconds(-20),
            Now.AddMinutes(-5));

        ReminderDelivery delivery = reminder.Queue(Now, TimeSpan.FromMinutes(1));

        Assert.False(delivery.WasMissed);
    }
}
