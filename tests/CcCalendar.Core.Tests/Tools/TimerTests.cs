using CcCalendar.Core.Tools;

namespace CcCalendar.Core.Tests.Tools;

public sealed class TimerTests
{
    [Fact]
    public void CountdownPausesWithoutLosingTimeAndCompletesOnce()
    {
        var timer = new CountdownTimer(TimeSpan.FromMinutes(10));
        var start = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

        timer.Start(start);
        timer.Pause(start.AddMinutes(3));
        timer.Start(start.AddMinutes(8));

        Assert.Equal(TimeSpan.FromMinutes(7), timer.GetRemaining(start.AddMinutes(8)));
        Assert.False(timer.Tick(start.AddMinutes(14)));
        Assert.True(timer.Tick(start.AddMinutes(15)));
        Assert.False(timer.Tick(start.AddMinutes(16)));
    }

    [Fact]
    public void PomodoroCompletesFocusAndSwitchesToBreak()
    {
        var timer = new PomodoroTimer(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5));
        var start = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

        timer.Start(start);
        CompletedFocusSession? completed = timer.Tick(start.AddMinutes(25));

        Assert.NotNull(completed);
        Assert.Equal(TimeSpan.FromMinutes(25), completed.Duration);
        Assert.Equal(PomodoroPhase.Break, timer.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.GetRemaining(start.AddMinutes(25)));
    }
}
