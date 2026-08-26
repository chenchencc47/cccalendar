namespace CcCalendar.Core.Tools;

public readonly record struct CaptureRegion
{
    public CaptureRegion(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public static CaptureRegion FromDrag(int startX, int startY, int endX, int endY)
    {
        int x = Math.Min(startX, endX);
        int y = Math.Min(startY, endY);
        int width = Math.Abs(endX - startX);
        int height = Math.Abs(endY - startY);
        if (width == 0 || height == 0)
        {
            throw new ArgumentException("A capture region must have a positive width and height.");
        }

        return new CaptureRegion(x, y, width, height);
    }
}
