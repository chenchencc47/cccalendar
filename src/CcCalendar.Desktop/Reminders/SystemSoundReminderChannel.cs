using System.Media;
using CcCalendar.Core.Reminders;

namespace CcCalendar.Desktop.Reminders;

public sealed class SystemSoundReminderChannel : IReminderNotificationChannel
{
    public Task ShowAsync(ReminderDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SystemSounds.Asterisk.Play();
        return Task.CompletedTask;
    }
}
