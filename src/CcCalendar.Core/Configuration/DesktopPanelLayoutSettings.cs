namespace CcCalendar.Core.Configuration;

public sealed record DesktopPanelLayoutSettings
{
    public double Width { get; init; } = 320;

    public double Height { get; init; } = 300;

    public double? Left { get; init; }

    public double? Top { get; init; }
}
