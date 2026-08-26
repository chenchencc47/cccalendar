using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class DesktopPanelLayoutState
{
    private DesktopPanelLayoutSettings settings;

    public DesktopPanelLayoutState(DesktopPanelLayoutSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public DesktopPanelLayoutSettings Current => settings;

    public void RememberBounds(double width, double height, double left, double top)
    {
        settings = settings with
        {
            Width = width,
            Height = height,
            Left = left,
            Top = top,
        };
    }

    public DesktopPanelLayoutSettings ToSettings() => settings;
}
