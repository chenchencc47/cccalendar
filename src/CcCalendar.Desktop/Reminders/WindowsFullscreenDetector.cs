using System.Runtime.InteropServices;

namespace CcCalendar.Desktop.Reminders;

public sealed class WindowsFullscreenDetector : IFullscreenDetector
{
    private const uint NearestMonitor = 2;

    public bool IsForegroundFullscreen()
    {
        nint window = GetForegroundWindow();
        if (window == nint.Zero || IsIconic(window))
        {
            return false;
        }

        if (window == FindWindow("Progman", null)
            || window == FindWindow("Shell_TrayWnd", null))
        {
            return false;
        }

        nint monitor = MonitorFromWindow(window, NearestMonitor);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return monitor != nint.Zero
            && GetWindowRect(window, out NativeRect windowRect)
            && GetMonitorInfo(monitor, ref monitorInfo)
            && Covers(windowRect, monitorInfo.Monitor);
    }

    private static bool Covers(NativeRect window, NativeRect monitor)
    {
        const int tolerance = 2;
        return window.Left <= monitor.Left + tolerance
            && window.Top <= monitor.Top + tolerance
            && window.Right >= monitor.Right - tolerance
            && window.Bottom >= monitor.Bottom - tolerance;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
