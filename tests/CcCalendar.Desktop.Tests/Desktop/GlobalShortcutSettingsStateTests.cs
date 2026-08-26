using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class GlobalShortcutSettingsStateTests
{
    [Fact]
    public void UpdateRejectsDuplicateAndModifierlessGestures()
    {
        var state = new GlobalShortcutSettingsState(new GlobalShortcutSettings());

        ShortcutUpdateResult duplicate = state.TryUpdate(
            GlobalShortcutAction.QuickAdd,
            state.GetGesture(GlobalShortcutAction.ToggleQuickPanel));
        ShortcutUpdateResult modifierless = state.TryUpdate(
            GlobalShortcutAction.QuickAdd,
            new ShortcutGestureSettings { Key = "F9" });

        Assert.Equal(ShortcutConflictKind.Duplicate, duplicate.Conflict);
        Assert.Equal(GlobalShortcutAction.ToggleQuickPanel, duplicate.ConflictingAction);
        Assert.Equal(ShortcutConflictKind.Invalid, modifierless.Conflict);
    }

    [Fact]
    public void ValidUpdateChangesOnlyRequestedAction()
    {
        var original = new GlobalShortcutSettings();
        var state = new GlobalShortcutSettingsState(original);
        var replacement = new ShortcutGestureSettings
        {
            Control = true,
            Alt = true,
            Shift = true,
            Key = "K",
        };

        ShortcutUpdateResult result = state.TryUpdate(
            GlobalShortcutAction.ToggleDesktopWorkbench,
            replacement);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacement, state.GetGesture(GlobalShortcutAction.ToggleDesktopWorkbench));
        Assert.Equal(original.ToggleQuickPanel, state.GetGesture(GlobalShortcutAction.ToggleQuickPanel));
    }
}
