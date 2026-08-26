using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class DesktopCalendarLayoutState
{
    private DesktopCalendarLayoutSettings settings;

    public DesktopCalendarLayoutState(DesktopCalendarLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
    }

    public DesktopCalendarMode Mode => settings.Mode;

    public bool IsTopCenterPinned => settings.IsTopCenterPinned;

    public DesktopCalendarSize CurrentSize => GetSize(settings.Mode);

    public void RememberSize(double width, double height)
    {
        settings = settings.Mode switch
        {
            DesktopCalendarMode.Month => settings with
            {
                MonthWidth = width,
                MonthHeight = height,
            },
            DesktopCalendarMode.Week => settings with
            {
                WeekWidth = width,
                WeekHeight = height,
            },
            _ => throw new InvalidOperationException("Unsupported desktop calendar mode."),
        };
    }

    public DesktopCalendarSize SwitchTo(DesktopCalendarMode mode)
    {
        settings = settings with { Mode = mode };
        return GetSize(mode);
    }

    public void RememberPosition(double left, double top)
    {
        settings = settings with { Left = left, Top = top };
    }

    public void RememberHorizontalPosition(double left)
    {
        settings = settings with { Left = left };
    }

    public void ToggleTopCenterPinned()
    {
        settings = settings with { IsTopCenterPinned = !settings.IsTopCenterPinned };
    }

    public DesktopCalendarLayoutSettings ToSettings() => settings;

    private DesktopCalendarSize GetSize(DesktopCalendarMode mode)
    {
        return mode switch
        {
            DesktopCalendarMode.Month => new DesktopCalendarSize(
                settings.MonthWidth,
                settings.MonthHeight),
            DesktopCalendarMode.Week => new DesktopCalendarSize(
                settings.WeekWidth,
                settings.WeekHeight),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}

public readonly record struct DesktopCalendarSize(double Width, double Height);
