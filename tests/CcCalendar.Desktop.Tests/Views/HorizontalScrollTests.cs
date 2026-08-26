using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop.Tests.Views;

public sealed class HorizontalScrollTests
{
    [Fact]
    public void DraggingContentToTheRightMovesTheContentWithThePointer()
    {
        Assert.Equal(0, HorizontalScroll.CalculatePanOffset(0, 120, 600));
        Assert.Equal(80, HorizontalScroll.CalculatePanOffset(200, 120, 600));
    }

    [Fact]
    public void DraggingContentToTheLeftMovesForwardThroughTheTimeline()
    {
        Assert.Equal(320, HorizontalScroll.CalculatePanOffset(200, -120, 600));
    }
}
