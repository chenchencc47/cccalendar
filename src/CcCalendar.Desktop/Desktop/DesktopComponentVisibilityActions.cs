namespace CcCalendar.Desktop.Desktop;

public sealed class DesktopComponentVisibilityActions(
    Action<DesktopComponentKind> toggle,
    Func<DesktopComponentKind, bool> isVisible)
{
    public void Toggle(DesktopComponentKind kind) => toggle(kind);

    public bool IsVisible(DesktopComponentKind kind) => isVisible(kind);
}
