using CcCalendar.Core.Reminders;

namespace CcCalendar.Desktop.Reminders;

public sealed class PopupReminderChannel : IReminderNotificationChannel, IDisposable
{
    private readonly Func<ReminderDelivery, Task> acknowledge;
    private readonly Func<int> defaultSnoozeMinutes;
    private readonly Queue<ReminderDelivery> pending = [];
    private readonly Func<ReminderDelivery, TimeSpan, Task> snooze;
    private ReminderPopupWindow? currentWindow;
    private bool isDisposed;

    public PopupReminderChannel(
        Func<int> defaultSnoozeMinutes,
        Func<ReminderDelivery, Task> acknowledge,
        Func<ReminderDelivery, TimeSpan, Task> snooze)
    {
        this.defaultSnoozeMinutes = defaultSnoozeMinutes;
        this.acknowledge = acknowledge;
        this.snooze = snooze;
    }

    public Task ShowAsync(ReminderDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        pending.Enqueue(delivery);
        ShowNext();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        isDisposed = true;
        pending.Clear();
        currentWindow?.CloseWithoutAction();
        currentWindow = null;
        GC.SuppressFinalize(this);
    }

    private void ShowNext()
    {
        if (isDisposed || currentWindow is not null || pending.Count == 0)
        {
            return;
        }

        ReminderDelivery delivery = pending.Dequeue();
        currentWindow = new ReminderPopupWindow(
            delivery,
            defaultSnoozeMinutes(),
            acknowledge,
            snooze);
        currentWindow.Closed += (_, _) =>
        {
            currentWindow = null;
            ShowNext();
        };
        currentWindow.Show();
    }
}
