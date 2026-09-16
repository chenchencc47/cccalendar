namespace CcCalendar.Infrastructure.Reminders;

public sealed class ReminderBackgroundScheduler : IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly IReminderDispatcher dispatcher;
    private readonly TimeSpan missedGracePeriod;
    private readonly TimeSpan scanInterval;
    private readonly TimeProvider timeProvider;
    private Task? backgroundTask;

    public ReminderBackgroundScheduler(
        IReminderDispatcher dispatcher,
        TimeProvider timeProvider,
        TimeSpan scanInterval,
        TimeSpan missedGracePeriod)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scanInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(missedGracePeriod, TimeSpan.Zero);
        this.dispatcher = dispatcher;
        this.timeProvider = timeProvider;
        this.scanInterval = scanInterval;
        this.missedGracePeriod = missedGracePeriod;
    }

    public Exception? LastError { get; private set; }

    public void Start()
    {
        // The scheduler performs database I/O and must not inherit the WPF UI
        // synchronization context. Otherwise shutdown can synchronously wait
        // for a continuation that is queued behind the closing UI thread.
        backgroundTask ??= Task.Run(
            () => RunAsync(cancellation.Token),
            cancellation.Token);
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        return await dispatcher.DispatchDueAsync(
            timeProvider.GetUtcNow(),
            missedGracePeriod,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await cancellation.CancelAsync();
        if (backgroundTask is not null)
        {
            await backgroundTask;
        }

        cancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await DispatchSafelyAsync(cancellationToken);
        using var timer = new PeriodicTimer(scanInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await DispatchSafelyAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task DispatchSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunOnceAsync(cancellationToken);
            LastError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LastError = exception;
        }
    }
}
