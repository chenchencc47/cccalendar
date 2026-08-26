using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public interface IGlobalHotkeyRegistry
{
    bool TryRegister(GlobalShortcutAction action, ShortcutGestureSettings gesture);

    bool TryReplace(
        GlobalShortcutAction action,
        ShortcutGestureSettings current,
        ShortcutGestureSettings replacement);
}
