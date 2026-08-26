using CcCalendar.Core.Configuration;
using CcCalendar.Core.Tools;

namespace CcCalendar.Core.Tests.Tools;

public sealed class WellnessReminderSchedulerTests
{
    [Fact]
    public void ChimeAndSedentaryRemindersFireOnceAndRespectActivityReset()
    {
        var settings = new WellnessReminderSettings
        {
            HourlyChimeEnabled = true,
            SedentaryReminderEnabled = true,
            SedentaryIntervalMinutes = 60,
            ActiveStart = new TimeOnly(8, 0),
            ActiveEnd = new TimeOnly(18, 0),
        };
        var start = new DateTimeOffset(2026, 8, 17, 8, 15, 0, TimeSpan.FromHours(8));
        var scheduler = new WellnessReminderScheduler(start);

        Assert.Equal(
            [WellnessReminderKind.HourlyChime],
            scheduler.Evaluate(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.FromHours(8)), settings));
        Assert.Empty(scheduler.Evaluate(
            new DateTimeOffset(2026, 8, 17, 9, 0, 30, TimeSpan.FromHours(8)),
            settings));
        Assert.Equal(
            [WellnessReminderKind.Sedentary],
            scheduler.Evaluate(new DateTimeOffset(2026, 8, 17, 9, 15, 0, TimeSpan.FromHours(8)), settings));

        scheduler.RecordActivity(new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(8)));

        Assert.Empty(scheduler.Evaluate(
            new DateTimeOffset(2026, 8, 17, 10, 15, 0, TimeSpan.FromHours(8)),
            settings));
        Assert.Equal(
            [WellnessReminderKind.Sedentary],
            scheduler.Evaluate(new DateTimeOffset(2026, 8, 17, 10, 30, 0, TimeSpan.FromHours(8)), settings));
        Assert.Empty(scheduler.Evaluate(
            new DateTimeOffset(2026, 8, 17, 19, 0, 0, TimeSpan.FromHours(8)),
            settings));
    }
}
