namespace CcCalendar.Desktop.Desktop;

public static class DesktopEdgeHideLayout
{
    public static DesktopWindowPosition GetHiddenPosition(
        DesktopEdge edge,
        DesktopWindowBounds shownBounds,
        DesktopWindowBounds workArea,
        double revealSize)
    {
        return edge switch
        {
            DesktopEdge.Left => new DesktopWindowPosition(
                workArea.Left - shownBounds.Width + revealSize,
                shownBounds.Top),
            DesktopEdge.Top => new DesktopWindowPosition(
                shownBounds.Left,
                workArea.Top - shownBounds.Height + revealSize),
            DesktopEdge.Right => new DesktopWindowPosition(
                workArea.Right - revealSize,
                shownBounds.Top),
            DesktopEdge.Bottom => new DesktopWindowPosition(
                shownBounds.Left,
                workArea.Bottom - revealSize),
            _ => new DesktopWindowPosition(shownBounds.Left, shownBounds.Top),
        };
    }

    public static DesktopEdge FindEdge(
        DesktopWindowBounds bounds,
        DesktopWindowBounds workArea,
        double threshold)
    {
        if (Math.Abs(bounds.Left - workArea.Left) <= threshold)
        {
            return DesktopEdge.Left;
        }

        if (Math.Abs(bounds.Top - workArea.Top) <= threshold)
        {
            return DesktopEdge.Top;
        }

        if (Math.Abs(bounds.Right - workArea.Right) <= threshold)
        {
            return DesktopEdge.Right;
        }

        if (Math.Abs(bounds.Bottom - workArea.Bottom) <= threshold)
        {
            return DesktopEdge.Bottom;
        }

        return DesktopEdge.None;
    }
}

public enum DesktopEdge
{
    None,
    Left,
    Top,
    Right,
    Bottom,
}

public readonly record struct DesktopWindowBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public readonly record struct DesktopWindowPosition(double Left, double Top);
