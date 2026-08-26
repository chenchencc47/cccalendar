using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public interface IDesktopWindowBehaviorTarget
{
    DesktopWindowBehaviorSettings BehaviorSettings { get; }

    void TogglePositionLock();

    void ToggleMousePassthrough();

    void DisableMousePassthrough();

    void SetLayer(DesktopWindowLayer layer);

    void ToggleEdgeAutoHide();
}
