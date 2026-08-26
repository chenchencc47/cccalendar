using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class GlobalShortcutSettingsState
{
    private GlobalShortcutSettings settings;

    public GlobalShortcutSettingsState(GlobalShortcutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
    }

    public ShortcutGestureSettings GetGesture(GlobalShortcutAction action)
    {
        return action switch
        {
            GlobalShortcutAction.OpenMainWindow => settings.OpenMainWindow,
            GlobalShortcutAction.ToggleQuickPanel => settings.ToggleQuickPanel,
            GlobalShortcutAction.QuickAdd => settings.QuickAdd,
            GlobalShortcutAction.ToggleDesktopWorkbench => settings.ToggleDesktopWorkbench,
            GlobalShortcutAction.DisableMousePassthrough => settings.DisableMousePassthrough,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    public ShortcutUpdateResult TryUpdate(
        GlobalShortcutAction action,
        ShortcutGestureSettings gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        if (!IsValid(gesture))
        {
            return new ShortcutUpdateResult(ShortcutConflictKind.Invalid);
        }

        foreach (GlobalShortcutAction otherAction in Enum.GetValues<GlobalShortcutAction>())
        {
            if (otherAction != action && GetGesture(otherAction) == gesture)
            {
                return new ShortcutUpdateResult(
                    ShortcutConflictKind.Duplicate,
                    otherAction);
            }
        }

        settings = action switch
        {
            GlobalShortcutAction.OpenMainWindow => settings with { OpenMainWindow = gesture },
            GlobalShortcutAction.ToggleQuickPanel => settings with { ToggleQuickPanel = gesture },
            GlobalShortcutAction.QuickAdd => settings with { QuickAdd = gesture },
            GlobalShortcutAction.ToggleDesktopWorkbench => settings with
            {
                ToggleDesktopWorkbench = gesture,
            },
            GlobalShortcutAction.DisableMousePassthrough => settings with
            {
                DisableMousePassthrough = gesture,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        return ShortcutUpdateResult.Success;
    }

    public GlobalShortcutSettings ToSettings() => settings;

    private static bool IsValid(ShortcutGestureSettings gesture)
    {
        return gesture.HasModifier && !string.IsNullOrWhiteSpace(gesture.Key);
    }
}
