using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class DesktopWindowBehaviorState
{
    private DesktopWindowBehaviorSettings settings;

    public DesktopWindowBehaviorState(DesktopWindowBehaviorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
    }

    public void TogglePositionLock()
    {
        settings = settings with { IsPositionLocked = !settings.IsPositionLocked };
    }

    public void ToggleMousePassthrough()
    {
        settings = settings with { IsMousePassthrough = !settings.IsMousePassthrough };
    }

    public void DisableMousePassthrough()
    {
        settings = settings with { IsMousePassthrough = false };
    }

    public void SetLayer(DesktopWindowLayer layer)
    {
        settings = settings with { Layer = layer };
    }

    public void ToggleEdgeAutoHide()
    {
        settings = settings with
        {
            IsEdgeAutoHideEnabled = !settings.IsEdgeAutoHideEnabled,
        };
    }

    public DesktopWindowBehaviorSettings ToSettings() => settings;
}
