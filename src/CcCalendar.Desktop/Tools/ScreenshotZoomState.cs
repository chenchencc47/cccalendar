namespace CcCalendar.Desktop.Tools;

public readonly record struct ScreenshotZoomState
{
    public const int MinimumPercent = 25;
    public const int MaximumPercent = 400;
    public const int StepPercent = 25;

    public ScreenshotZoomState()
        : this(100)
    {
    }

    public ScreenshotZoomState(int percent)
    {
        Percent = Math.Clamp(percent, MinimumPercent, MaximumPercent);
    }

    public int Percent { get; }

    public double Scale => Percent / 100d;

    public ScreenshotZoomState SetPercent(int percent) =>
        percent == Percent ? this : new(percent);

    public ScreenshotZoomState Step(int direction) =>
        SetPercent(Percent + Math.Sign(direction) * StepPercent);
}
