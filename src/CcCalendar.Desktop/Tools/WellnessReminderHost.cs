using System.Media;
using System.Windows.Threading;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Tools;

namespace CcCalendar.Desktop.Tools;

public sealed class WellnessReminderHost : IDisposable
{
    private readonly Func<WellnessReminderSettings> getSettings;
    private readonly Action<string, string> notify;
    private readonly WellnessReminderScheduler scheduler;
    private readonly TimeProvider timeProvider;
    private readonly DispatcherTimer timer;

    public WellnessReminderHost(
        WellnessReminderScheduler scheduler,
        TimeProvider timeProvider,
        Func<WellnessReminderSettings> getSettings,
        Action<string, string> notify)
    {
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
        this.notify = notify ?? throw new ArgumentNullException(nameof(notify));
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += Tick;
    }

    public void Start() => timer.Start();

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= Tick;
    }

    private void Tick(object? sender, EventArgs e)
    {
        DateTimeOffset localNow = timeProvider.GetLocalNow();
        foreach (WellnessReminderKind reminder in scheduler.Evaluate(localNow, getSettings()))
        {
            if (reminder == WellnessReminderKind.HourlyChime)
            {
                notify("整点报时", $"现在是 {localNow:HH:mm}");
                SystemSounds.Asterisk.Play();
            }
            else
            {
                notify("久坐提醒", "起身活动一下，再继续工作。");
                SystemSounds.Exclamation.Play();
            }
        }
    }
}
