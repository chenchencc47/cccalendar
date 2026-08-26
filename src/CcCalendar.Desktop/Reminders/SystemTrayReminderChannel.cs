using CcCalendar.Core.Reminders;

namespace CcCalendar.Desktop.Reminders;

public sealed class SystemTrayReminderChannel(Action<string, string> showNotification)
    : IReminderNotificationChannel
{
    public Task ShowAsync(ReminderDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        showNotification(
            delivery.WasMissed ? "错过提醒" : "提醒",
            delivery.Title);
        return Task.CompletedTask;
    }
}
