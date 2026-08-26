namespace CcCalendar.Core.Configuration;

public sealed record DesktopCalendarLayoutSettings
{
    public DesktopCalendarMode Mode { get; init; } = DesktopCalendarMode.Month;

    public double MonthWidth { get; init; } = 680;

    public double MonthHeight { get; init; } = 460;

    public double WeekWidth { get; init; } = 680;

    public double WeekHeight { get; init; } = 260;

    public double? Left { get; init; }

    public double? Top { get; init; }

    public bool IsTopCenterPinned { get; init; } = true;
}
