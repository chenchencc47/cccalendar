using System.Windows;

namespace CcCalendar.Desktop.Desktop;

public static class QuickPanelPositioner
{
    public static Point Calculate(Rect workArea, Size panelSize, double margin)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(margin);
        return new Point(
            workArea.Right - panelSize.Width - margin,
            workArea.Bottom - panelSize.Height - margin);
    }
}
