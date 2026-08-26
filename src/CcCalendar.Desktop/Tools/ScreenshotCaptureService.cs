using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CcCalendar.Core.Tools;

namespace CcCalendar.Desktop.Tools;

public static class ScreenshotCaptureService
{
    public static BitmapSource CapturePrimaryScreen()
    {
        Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        return CaptureRegion(new CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    public static BitmapSource CaptureRegion(CaptureRegion region)
    {
        using var bitmap = new Bitmap(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(region.X, region.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        return ToBitmapSource(bitmap);
    }

    public static BitmapSource CaptureWindow(nint windowHandle)
    {
        if (!GetWindowRect(windowHandle, out NativeRect rect))
        {
            throw new InvalidOperationException("无法读取窗口区域。");
        }

        return CaptureRegion(new CaptureRegion(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top));
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        nint handle = bitmap.GetHbitmap();
        try
        {
            BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                nint.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rect);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
