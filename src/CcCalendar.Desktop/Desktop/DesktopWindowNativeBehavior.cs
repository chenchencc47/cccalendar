using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

internal static class DesktopWindowNativeBehavior
{
    private const int ExtendedStyleIndex = -20;
    private const long TransparentExtendedStyle = 0x00000020L;
    private const uint NoActivate = 0x0010;
    private const uint NoMove = 0x0002;
    private const uint NoOwnerZOrder = 0x0200;
    private const uint NoSize = 0x0001;

    private static readonly nint BottomWindow = new(1);
    private static readonly nint NotTopmostWindow = new(-2);

    public static void ApplyMousePassthrough(Window window, bool enabled)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        nint currentStyle = GetWindowLongPtr(handle, ExtendedStyleIndex);
        long style = currentStyle.ToInt64();
        long updatedStyle = enabled
            ? style | TransparentExtendedStyle
            : style & ~TransparentExtendedStyle;
        if (updatedStyle != style)
        {
            SetWindowLongPtr(handle, ExtendedStyleIndex, new nint(updatedStyle));
        }
    }

    public static void ApplyLayer(Window window, DesktopWindowLayer layer)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        window.Topmost = layer == DesktopWindowLayer.Topmost;
        if (layer == DesktopWindowLayer.Desktop)
        {
            SetWindowPos(
                handle,
                BottomWindow,
                0,
                0,
                0,
                0,
                NoActivate | NoMove | NoOwnerZOrder | NoSize);
        }
        else if (layer == DesktopWindowLayer.Normal)
        {
            SetWindowPos(
                handle,
                NotTopmostWindow,
                0,
                0,
                0,
                0,
                NoActivate | NoMove | NoOwnerZOrder | NoSize);
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
