using CcCalendar.Core.Tools;

namespace CcCalendar.Core.Tests.Tools;

public sealed class CaptureRegionTests
{
    [Fact]
    public void DragPointsNormalizeToPositivePixelRegion()
    {
        CaptureRegion region = CaptureRegion.FromDrag(900, 700, 100, 200);

        Assert.Equal(100, region.X);
        Assert.Equal(200, region.Y);
        Assert.Equal(800, region.Width);
        Assert.Equal(500, region.Height);
        Assert.Throws<ArgumentException>(() => CaptureRegion.FromDrag(1, 1, 1, 20));
    }
}
