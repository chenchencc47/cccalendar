using System.Windows;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class QuickPanelPositionerTests
{
    [Fact]
    public void CalculateAnchorsPanelToBottomRightWithinWorkArea()
    {
        var workArea = new Rect(0, 0, 1920, 1040);
        var panelSize = new Size(420, 640);

        Point position = QuickPanelPositioner.Calculate(workArea, panelSize, 12);

        Assert.Equal(new Point(1488, 388), position);
    }
}
