using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CcCalendar.Desktop.Tools;

public sealed class ClipboardUpdateListener : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private readonly ClipboardCaptureCoordinator coordinator;
    private readonly SemaphoreSlim captureGate = new(1, 1);
    private readonly Window window;
    private HwndSource? source;
    private nint windowHandle;

    public ClipboardUpdateListener(Window window, ClipboardCaptureCoordinator coordinator)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        window.SourceInitialized += SourceInitialized;
    }

    public void Dispose()
    {
        window.SourceInitialized -= SourceInitialized;
        if (source is not null)
        {
            source.RemoveHook(WindowProcedure);
            source = null;
        }

        if (windowHandle != 0)
        {
            RemoveClipboardFormatListener(windowHandle);
            windowHandle = 0;
        }

        captureGate.Dispose();
    }

    private void SourceInitialized(object? sender, EventArgs e)
    {
        windowHandle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(windowHandle);
        source?.AddHook(WindowProcedure);
        if (!AddClipboardFormatListener(windowHandle))
        {
            throw new InvalidOperationException("Unable to register the clipboard listener.");
        }
    }

    private nint WindowProcedure(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmClipboardUpdate)
        {
            _ = CaptureAsync();
        }

        return 0;
    }

    private async Task CaptureAsync()
    {
        await captureGate.WaitAsync();
        try
        {
            await coordinator.CaptureCurrentAsync(CancellationToken.None);
        }
        catch (ExternalException)
        {
            // Another process can briefly own the clipboard; the next update retries naturally.
        }
        finally
        {
            captureGate.Release();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);
}
