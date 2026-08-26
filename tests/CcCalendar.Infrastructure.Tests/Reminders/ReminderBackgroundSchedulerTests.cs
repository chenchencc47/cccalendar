using CcCalendar.Infrastructure.Reminders;

namespace CcCalendar.Infrastructure.Tests.Reminders;

public sealed class ReminderBackgroundSchedulerTests
{
    [Fact]
    public async Task StartImmediatelyDispatchesUsingCurrentUtcTime()
    {
        var dispatcher = new RecordingDispatcher();
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        await using var scheduler = new ReminderBackgroundScheduler(
            dispatcher,
            new FixedTimeProvider(now),
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(1));

        scheduler.Start();
        DispatchInvocation invocation = await dispatcher.FirstInvocation.Task.WaitAsync(
            TimeSpan.FromSeconds(1));

        Assert.Equal(now, invocation.NowUtc);
        Assert.Equal(TimeSpan.FromMinutes(1), invocation.MissedGracePeriod);
    }

    private sealed class RecordingDispatcher : IReminderDispatcher
    {
        public TaskCompletionSource<DispatchInvocation> FirstInvocation { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> DispatchDueAsync(
            DateTimeOffset nowUtc,
            TimeSpan missedGracePeriod,
            CancellationToken cancellationToken)
        {
            FirstInvocation.TrySetResult(new DispatchInvocation(nowUtc, missedGracePeriod));
            return Task.FromResult(0);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record DispatchInvocation(
        DateTimeOffset NowUtc,
        TimeSpan MissedGracePeriod);
}
