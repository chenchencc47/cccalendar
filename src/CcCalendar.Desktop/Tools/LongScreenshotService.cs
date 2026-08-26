using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace CcCalendar.Desktop.Tools;

public static class LongScreenshotService
{
    private const uint MouseWheelMessage = 0x020A;

    public static async Task<BitmapSource> CaptureWindowAsync(
        nint windowHandle,
        int pageCount,
        int overlap,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 2);
        var pages = new List<BitmapSource>();
        for (int page = 0; page < pageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(ScreenshotCaptureService.CaptureWindow(windowHandle));
            if (page < pageCount - 1)
            {
                for (int step = 0; step < 5; step++)
                {
                    PostMessage(windowHandle, MouseWheelMessage, new nint(-120 << 16), nint.Zero);
                }

                await Task.Delay(250, cancellationToken);
            }
        }

        return LongScreenshotComposer.Stitch(pages, overlap);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint windowHandle, uint message, nint wParam, nint lParam);
}
