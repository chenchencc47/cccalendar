using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CcCalendar.Core.Tools;

namespace CcCalendar.Desktop.Tools;

public static class LongScreenshotComposer
{
    public static BitmapSource Stitch(IReadOnlyList<BitmapSource> pages, int overlap)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        LongCaptureLayout layout = LongCaptureLayout.Create(
            pages.Select(page => page.PixelHeight).ToArray(),
            overlap);
        int width = pages.Max(page => page.PixelWidth);
        var visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            for (int index = 0; index < pages.Count; index++)
            {
                BitmapSource page = pages[index];
                drawing.DrawImage(
                    page,
                    new Rect(0, layout.TopOffsets[index], page.PixelWidth, page.PixelHeight));
            }
        }

        var result = new RenderTargetBitmap(
            width,
            layout.TotalHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }
}
