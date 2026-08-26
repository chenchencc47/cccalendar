namespace CcCalendar.Core.Tools;

public enum PomodoroPhase
{
    Focus,
    Break,
}

public sealed record CompletedFocusSession(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    TimeSpan Duration);

public sealed class PomodoroTimer
{
    private readonly TimeSpan breakDuration;
    private CountdownTimer countdown;
    private readonly TimeSpan focusDuration;
    private DateTimeOffset? focusStartedAtUtc;

    public PomodoroTimer(TimeSpan focusDuration, TimeSpan breakDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(focusDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(breakDuration, TimeSpan.Zero);
        this.focusDuration = focusDuration;
        this.breakDuration = breakDuration;
        countdown = new CountdownTimer(focusDuration);
    }

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;

    public TimerState State => countdown.State;

    public void Start(DateTimeOffset nowUtc)
    {
        if (Phase == PomodoroPhase.Focus && countdown.State == TimerState.Ready)
        {
            focusStartedAtUtc = nowUtc.ToUniversalTime();
        }

        countdown.Start(nowUtc);
    }

    public void Pause(DateTimeOffset nowUtc) => countdown.Pause(nowUtc);

    public TimeSpan GetRemaining(DateTimeOffset nowUtc) => countdown.GetRemaining(nowUtc);

    public CompletedFocusSession? Tick(DateTimeOffset nowUtc)
    {
        if (!countdown.Tick(nowUtc))
        {
            return null;
        }

        DateTimeOffset completedAtUtc = nowUtc.ToUniversalTime();
        if (Phase == PomodoroPhase.Focus)
        {
            var completed = new CompletedFocusSession(
                focusStartedAtUtc ?? completedAtUtc - focusDuration,
                completedAtUtc,
                focusDuration);
            Phase = PomodoroPhase.Break;
            countdown = new CountdownTimer(breakDuration);
            countdown.Start(nowUtc);
            focusStartedAtUtc = null;
            return completed;
        }

        Phase = PomodoroPhase.Focus;
        countdown = new CountdownTimer(focusDuration);
        return null;
    }

    public void Reset()
    {
        Phase = PomodoroPhase.Focus;
        countdown = new CountdownTimer(focusDuration);
        focusStartedAtUtc = null;
    }
}
