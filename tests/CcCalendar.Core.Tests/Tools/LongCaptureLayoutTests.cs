using CcCalendar.Core.Tools;

namespace CcCalendar.Core.Tests.Tools;

public sealed class LongCaptureLayoutTests
{
    [Fact]
    public void LayoutStitchesPagesUsingConfiguredOverlap()
    {
        LongCaptureLayout layout = LongCaptureLayout.Create([800, 800, 600], 100);

        Assert.Equal([0, 700, 1400], layout.TopOffsets);
        Assert.Equal(2000, layout.TotalHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => LongCaptureLayout.Create([100], 100));
    }
}
