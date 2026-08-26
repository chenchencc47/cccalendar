using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class ShortcutSettingsViewModelTests
{
    [Fact]
    public void SystemConflictRollsBackDisplayedAndPersistedGesture()
    {
        var registry = new RejectingReplacementRegistry();
        var viewModel = new ShortcutSettingsViewModel(
            new GlobalShortcutSettings(),
            registry);
        ShortcutBindingViewModel binding = viewModel.Bindings.Single(
            item => item.Action == GlobalShortcutAction.QuickAdd);
        ShortcutGestureSettings original = binding.Gesture;

        binding.TryAssign(new ShortcutGestureSettings
        {
            Control = true,
            Alt = true,
            Shift = true,
            Key = "K",
        });

        Assert.Equal(original, binding.Gesture);
        Assert.Equal(original, viewModel.Settings.QuickAdd);
        Assert.Equal("已被 Windows 或其他应用占用", binding.StatusMessage);
    }

    [Fact]
    public void FormatterUsesStableModifierOrder()
    {
        string display = ShortcutGestureFormatter.Format(new ShortcutGestureSettings
        {
            Control = true,
            Alt = true,
            Shift = true,
            Windows = true,
            Key = "F9",
        });

        Assert.Equal("Ctrl + Alt + Shift + Win + F9", display);
    }

    private sealed class RejectingReplacementRegistry : IGlobalHotkeyRegistry
    {
        public bool TryRegister(GlobalShortcutAction action, ShortcutGestureSettings gesture)
        {
            return true;
        }

        public bool TryReplace(
            GlobalShortcutAction action,
            ShortcutGestureSettings current,
            ShortcutGestureSettings replacement)
        {
            return false;
        }
    }
}
