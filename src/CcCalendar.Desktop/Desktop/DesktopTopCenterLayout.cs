namespace CcCalendar.Desktop.Desktop;

public static class DesktopTopCenterLayout
{
    public static DesktopWindowPosition Calculate(
        DesktopWindowBounds workArea,
        double windowWidth)
    {
        double left = workArea.Left + (workArea.Width - windowWidth) / 2;
        return new DesktopWindowPosition(left, workArea.Top);
    }
}
