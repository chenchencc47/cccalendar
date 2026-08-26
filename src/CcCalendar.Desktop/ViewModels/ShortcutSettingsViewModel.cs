using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class ShortcutSettingsViewModel
{
    private readonly Action<GlobalShortcutSettings> settingsChanged;
    private readonly GlobalShortcutSettingsState state;
    private readonly IGlobalHotkeyRegistry registry;

    public ShortcutSettingsViewModel(
        GlobalShortcutSettings settings,
        IGlobalHotkeyRegistry registry,
        Action<GlobalShortcutSettings>? settingsChanged = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(registry);
        state = new GlobalShortcutSettingsState(settings);
        this.registry = registry;
        this.settingsChanged = settingsChanged ?? (_ => { });
        Bindings =
        [
            CreateBinding(GlobalShortcutAction.OpenMainWindow, "打开主窗口"),
            CreateBinding(GlobalShortcutAction.ToggleQuickPanel, "切换快速面板"),
            CreateBinding(GlobalShortcutAction.QuickAdd, "快速新增"),
            CreateBinding(GlobalShortcutAction.ToggleDesktopWorkbench, "切换组合工作台"),
            CreateBinding(GlobalShortcutAction.DisableMousePassthrough, "关闭全部鼠标穿透"),
        ];

        foreach (ShortcutBindingViewModel binding in Bindings)
        {
            if (!registry.TryRegister(binding.Action, binding.Gesture))
            {
                binding.SetSystemConflict();
            }
        }
    }

    public IReadOnlyList<ShortcutBindingViewModel> Bindings { get; }

    public GlobalShortcutSettings Settings => state.ToSettings();

    private ShortcutBindingViewModel CreateBinding(
        GlobalShortcutAction action,
        string label)
    {
        return new ShortcutBindingViewModel(
            action,
            label,
            state.GetGesture(action),
            TryAssign);
    }

    private ShortcutUpdateResult TryAssign(
        GlobalShortcutAction action,
        ShortcutGestureSettings gesture)
    {
        ShortcutGestureSettings current = state.GetGesture(action);
        ShortcutUpdateResult result = state.TryUpdate(action, gesture);
        if (!result.IsSuccess)
        {
            return result;
        }

        if (!registry.TryReplace(action, current, gesture))
        {
            state.TryUpdate(action, current);
            return new ShortcutUpdateResult(ShortcutConflictKind.System);
        }

        settingsChanged(state.ToSettings());
        return ShortcutUpdateResult.Success;
    }
}

public sealed class ShortcutBindingViewModel : ObservableObject
{
    private readonly Func<GlobalShortcutAction, ShortcutGestureSettings, ShortcutUpdateResult> update;
    private ShortcutGestureSettings gesture;
    private string statusMessage = string.Empty;

    public ShortcutBindingViewModel(
        GlobalShortcutAction action,
        string label,
        ShortcutGestureSettings gesture,
        Func<GlobalShortcutAction, ShortcutGestureSettings, ShortcutUpdateResult> update)
    {
        Action = action;
        Label = label;
        this.gesture = gesture;
        this.update = update;
    }

    public GlobalShortcutAction Action { get; }

    public string Label { get; }

    public ShortcutGestureSettings Gesture
    {
        get => gesture;
        private set
        {
            if (SetProperty(ref gesture, value))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string DisplayText => ShortcutGestureFormatter.Format(Gesture);

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public void TryAssign(ShortcutGestureSettings value)
    {
        ShortcutUpdateResult result = update(Action, value);
        if (result.IsSuccess)
        {
            Gesture = value;
            StatusMessage = string.Empty;
            return;
        }

        StatusMessage = result.Conflict switch
        {
            ShortcutConflictKind.Invalid => "需要至少一个修饰键和一个主键",
            ShortcutConflictKind.Duplicate => "与其他 cccalendar 快捷键重复",
            ShortcutConflictKind.System => "已被 Windows 或其他应用占用",
            _ => string.Empty,
        };
    }

    public void SetSystemConflict()
    {
        StatusMessage = "已被 Windows 或其他应用占用";
    }
}

public static class ShortcutGestureFormatter
{
    public static string Format(ShortcutGestureSettings gesture)
    {
        var parts = new List<string>(5);
        if (gesture.Control)
        {
            parts.Add("Ctrl");
        }

        if (gesture.Alt)
        {
            parts.Add("Alt");
        }

        if (gesture.Shift)
        {
            parts.Add("Shift");
        }

        if (gesture.Windows)
        {
            parts.Add("Win");
        }

        parts.Add(gesture.Key);
        return string.Join(" + ", parts);
    }
}
