using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class DesktopWindowBehaviorStateTests
{
    [Fact]
    public void CommandsUpdatePersistedWindowBehavior()
    {
        var state = new DesktopWindowBehaviorState(new DesktopWindowBehaviorSettings());

        state.TogglePositionLock();
        state.ToggleMousePassthrough();
        state.SetLayer(DesktopWindowLayer.Topmost);
        state.ToggleEdgeAutoHide();

        Assert.Equal(
            new DesktopWindowBehaviorSettings
            {
                IsPositionLocked = true,
                IsMousePassthrough = true,
                Layer = DesktopWindowLayer.Topmost,
                IsEdgeAutoHideEnabled = true,
            },
            state.ToSettings());

        state.DisableMousePassthrough();

        Assert.False(state.ToSettings().IsMousePassthrough);
    }

    [Theory]
    [InlineData(DesktopEdge.Left, -316, 100)]
    [InlineData(DesktopEdge.Top, 200, -236)]
    [InlineData(DesktopEdge.Right, 1916, 100)]
    [InlineData(DesktopEdge.Bottom, 200, 1076)]
    public void HiddenPositionLeavesFourPixelRevealStrip(
        DesktopEdge edge,
        double expectedLeft,
        double expectedTop)
    {
        var shownBounds = new DesktopWindowBounds(200, 100, 320, 240);
        var workArea = new DesktopWindowBounds(0, 0, 1920, 1080);

        DesktopWindowPosition position = DesktopEdgeHideLayout.GetHiddenPosition(
            edge,
            shownBounds,
            workArea,
            revealSize: 4);

        Assert.Equal(new DesktopWindowPosition(expectedLeft, expectedTop), position);
    }

    [Fact]
    public void FindEdgeUsesThresholdAndReturnsNoneAwayFromEdges()
    {
        var workArea = new DesktopWindowBounds(0, 0, 1920, 1080);

        DesktopEdge nearRight = DesktopEdgeHideLayout.FindEdge(
            new DesktopWindowBounds(1591, 200, 320, 240),
            workArea,
            threshold: 12);
        DesktopEdge centered = DesktopEdgeHideLayout.FindEdge(
            new DesktopWindowBounds(800, 400, 320, 240),
            workArea,
            threshold: 12);

        Assert.Equal(DesktopEdge.Right, nearRight);
        Assert.Equal(DesktopEdge.None, centered);
    }
}
