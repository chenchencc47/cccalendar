using CcCalendar.Desktop.Tools;

namespace CcCalendar.Desktop.Tests.Tools;

public sealed class ScreenshotZoomStateTests
{
    [Fact]
    public void ZoomClampsAndReportsScaleWithoutChangingSourceDimensions()
    {
        var zoom = new ScreenshotZoomState();

        Assert.Equal(1d, zoom.Scale);
        Assert.Equal(125, zoom.SetPercent(125).Percent);
        Assert.Equal(4d, zoom.SetPercent(900).Scale);
        Assert.Equal(0.25d, zoom.SetPercent(0).Scale);
    }

    [Fact]
    public void StepMovesByTwentyFivePercent()
    {
        var zoom = new ScreenshotZoomState(100);

        Assert.Equal(125, zoom.Step(1).Percent);
        Assert.Equal(75, zoom.Step(-1).Percent);
    }
}
