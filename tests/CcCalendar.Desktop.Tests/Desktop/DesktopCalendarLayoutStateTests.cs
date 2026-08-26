using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class DesktopCalendarLayoutStateTests
{
    [Fact]
    public void SwitchingModesRestoresEachModesLastSize()
    {
        var settings = new DesktopCalendarLayoutSettings
        {
            Mode = DesktopCalendarMode.Month,
            MonthWidth = 680,
            MonthHeight = 460,
            WeekWidth = 620,
            WeekHeight = 260,
        };
        var state = new DesktopCalendarLayoutState(settings);

        state.RememberSize(720, 500);
        state.RememberPosition(120, 80);
        DesktopCalendarSize weekSize = state.SwitchTo(DesktopCalendarMode.Week);
        state.RememberSize(650, 300);
        DesktopCalendarSize monthSize = state.SwitchTo(DesktopCalendarMode.Month);

        Assert.Equal(new DesktopCalendarSize(620, 260), weekSize);
        Assert.Equal(new DesktopCalendarSize(720, 500), monthSize);
        Assert.Equal(
            settings with
            {
                MonthWidth = 720,
                MonthHeight = 500,
                WeekWidth = 650,
                WeekHeight = 300,
                Left = 120,
                Top = 80,
            },
            state.ToSettings());
    }

    [Fact]
    public void TopCenterPinIsEnabledByDefaultAndCanBeToggled()
    {
        var state = new DesktopCalendarLayoutState(new DesktopCalendarLayoutSettings());

        Assert.True(state.IsTopCenterPinned);

        state.ToggleTopCenterPinned();

        Assert.False(state.ToSettings().IsTopCenterPinned);
    }

    [Fact]
    public void TopCenterPositionUsesWorkAreaCenterAndTop()
    {
        DesktopWindowPosition position = DesktopTopCenterLayout.Calculate(
            new DesktopWindowBounds(0, 0, 1920, 1040),
            windowWidth: 760);

        Assert.Equal(new DesktopWindowPosition(580, 0), position);
    }

    [Fact]
    public void RememberHorizontalPositionPreservesVerticalPosition()
    {
        var state = new DesktopCalendarLayoutState(new DesktopCalendarLayoutSettings
        {
            Left = 120,
            Top = 80,
        });

        state.RememberHorizontalPosition(640);

        Assert.Equal(640, state.ToSettings().Left);
        Assert.Equal(80, state.ToSettings().Top);
    }
}
