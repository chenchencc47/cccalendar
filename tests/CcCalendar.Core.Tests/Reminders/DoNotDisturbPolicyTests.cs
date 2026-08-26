using CcCalendar.Core.Configuration;
using CcCalendar.Core.Reminders;

namespace CcCalendar.Core.Tests.Reminders;

public sealed class DoNotDisturbPolicyTests
{
    private static readonly ReminderNotificationSettings Settings = new()
    {
        IsDoNotDisturbEnabled = true,
        DoNotDisturbStart = new TimeOnly(22, 0),
        DoNotDisturbEnd = new TimeOnly(8, 0),
        SuppressWhenFullscreen = true,
    };

    [Theory]
    [InlineData(23, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(12, false)]
    public void OvernightWindowSuppressesExpectedHours(int hour, bool expected)
    {
        var localNow = new DateTimeOffset(2026, 8, 16, hour, 0, 0, TimeSpan.FromHours(8));

        bool actual = DoNotDisturbPolicy.IsSuppressed(localNow, Settings, isFullscreen: false);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullscreenSuppressionWorksOutsideQuietHours()
    {
        var localNoon = new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.True(DoNotDisturbPolicy.IsSuppressed(localNoon, Settings, isFullscreen: true));
    }
}
