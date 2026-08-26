using CcCalendar.Core.Reminders;

namespace CcCalendar.Desktop.Reminders;

public interface IReminderNotificationChannel
{
    Task ShowAsync(ReminderDelivery delivery, CancellationToken cancellationToken);
}
