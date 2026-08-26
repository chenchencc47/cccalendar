using System.Windows.Threading;

namespace CcCalendar.Desktop.Reminders;

public sealed class ReminderNotificationHost : IDisposable
{
    private readonly ReminderNotificationCoordinator coordinator;
    private readonly DispatcherTimer timer;
    private bool isPolling;

    public ReminderNotificationHost(
        ReminderNotificationCoordinator coordinator,
        Dispatcher dispatcher,
        TimeSpan pollInterval)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        this.coordinator = coordinator;
        timer = new DispatcherTimer(
            pollInterval,
            DispatcherPriority.Background,
            TimerTick,
            dispatcher);
    }

    public Exception? LastError { get; private set; }

    public void Start()
    {
        timer.Start();
        _ = PollAsync();
    }

    public void Dispose()
    {
        timer.Stop();
        GC.SuppressFinalize(this);
    }

    private async void TimerTick(object? sender, EventArgs e)
    {
        await PollAsync();
    }

    private async Task PollAsync()
    {
        if (isPolling)
        {
            return;
        }

        isPolling = true;
        try
        {
            await coordinator.PollAsync(CancellationToken.None);
            LastError = null;
        }
        catch (Exception exception)
        {
            LastError = exception;
        }
        finally
        {
            isPolling = false;
        }
    }
}
