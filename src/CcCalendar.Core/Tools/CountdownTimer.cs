namespace CcCalendar.Core.Tools;

public enum TimerState
{
    Ready,
    Running,
    Paused,
    Completed,
}

public sealed class CountdownTimer
{
    private DateTimeOffset? startedAtUtc;
    private TimeSpan remainingWhenStopped;

    public CountdownTimer(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        Duration = duration;
        remainingWhenStopped = duration;
    }

    public TimeSpan Duration { get; }

    public TimerState State { get; private set; } = TimerState.Ready;

    public void Start(DateTimeOffset nowUtc)
    {
        if (State is TimerState.Running or TimerState.Completed)
        {
            return;
        }

        startedAtUtc = nowUtc.ToUniversalTime();
        State = TimerState.Running;
    }

    public void Pause(DateTimeOffset nowUtc)
    {
        if (State != TimerState.Running)
        {
            return;
        }

        remainingWhenStopped = GetRemaining(nowUtc);
        startedAtUtc = null;
        State = TimerState.Paused;
    }

    public void Reset()
    {
        startedAtUtc = null;
        remainingWhenStopped = Duration;
        State = TimerState.Ready;
    }

    public bool Tick(DateTimeOffset nowUtc)
    {
        if (State != TimerState.Running || GetRemaining(nowUtc) > TimeSpan.Zero)
        {
            return false;
        }

        remainingWhenStopped = TimeSpan.Zero;
        startedAtUtc = null;
        State = TimerState.Completed;
        return true;
    }

    public TimeSpan GetRemaining(DateTimeOffset nowUtc)
    {
        if (State != TimerState.Running || !startedAtUtc.HasValue)
        {
            return remainingWhenStopped;
        }

        TimeSpan remaining = remainingWhenStopped
            - (nowUtc.ToUniversalTime() - startedAtUtc.Value);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
