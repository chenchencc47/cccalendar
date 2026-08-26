using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public sealed class GlobalHotkeyManager : IGlobalHotkeyRegistry, IDisposable
{
    private const int HotkeyMessage = 0x0312;
    private const uint AltModifier = 0x0001;
    private const uint ControlModifier = 0x0002;
    private const uint ShiftModifier = 0x0004;
    private const uint WindowsModifier = 0x0008;
    private const uint NoRepeatModifier = 0x4000;

    private readonly IReadOnlyDictionary<GlobalShortcutAction, Action> callbacks;
    private readonly HwndSource messageSource;
    private bool isDisposed;

    public GlobalHotkeyManager(IReadOnlyDictionary<GlobalShortcutAction, Action> callbacks)
    {
        ArgumentNullException.ThrowIfNull(callbacks);
        this.callbacks = callbacks;
        var parameters = new HwndSourceParameters("cccalendar-global-hotkeys")
        {
            Height = 0,
            ParentWindow = new nint(-3),
            Width = 0,
            WindowStyle = 0,
        };
        messageSource = new HwndSource(parameters);
        messageSource.AddHook(WindowMessageHook);
    }

    public bool TryRegister(GlobalShortcutAction action, ShortcutGestureSettings gesture)
    {
        return TryConvert(gesture, out uint modifiers, out uint virtualKey)
            && RegisterHotKey(
                messageSource.Handle,
                GetIdentifier(action),
                modifiers | NoRepeatModifier,
                virtualKey);
    }

    public bool TryReplace(
        GlobalShortcutAction action,
        ShortcutGestureSettings current,
        ShortcutGestureSettings replacement)
    {
        int identifier = GetIdentifier(action);
        UnregisterHotKey(messageSource.Handle, identifier);
        if (TryRegister(action, replacement))
        {
            return true;
        }

        TryRegister(action, current);
        return false;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        foreach (GlobalShortcutAction action in Enum.GetValues<GlobalShortcutAction>())
        {
            UnregisterHotKey(messageSource.Handle, GetIdentifier(action));
        }

        messageSource.RemoveHook(WindowMessageHook);
        messageSource.Dispose();
        isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private nint WindowMessageHook(
        nint windowHandle,
        int message,
        nint wordParameter,
        nint longParameter,
        ref bool handled)
    {
        if (message != HotkeyMessage)
        {
            return nint.Zero;
        }

        int identifier = wordParameter.ToInt32();
        GlobalShortcutAction action = (GlobalShortcutAction)(identifier - GetIdentifierBase());
        if (callbacks.TryGetValue(action, out Action? callback))
        {
            callback();
            handled = true;
        }

        return nint.Zero;
    }

    private static bool TryConvert(
        ShortcutGestureSettings gesture,
        out uint modifiers,
        out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (!Enum.TryParse(gesture.Key, ignoreCase: true, out Key key))
        {
            return false;
        }

        if (gesture.Control)
        {
            modifiers |= ControlModifier;
        }

        if (gesture.Alt)
        {
            modifiers |= AltModifier;
        }

        if (gesture.Shift)
        {
            modifiers |= ShiftModifier;
        }

        if (gesture.Windows)
        {
            modifiers |= WindowsModifier;
        }

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return modifiers != 0 && virtualKey != 0;
    }

    private static int GetIdentifier(GlobalShortcutAction action)
    {
        return GetIdentifierBase() + (int)action;
    }

    private static int GetIdentifierBase() => 0xCC00;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        nint windowHandle,
        int identifier,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint windowHandle, int identifier);
}
