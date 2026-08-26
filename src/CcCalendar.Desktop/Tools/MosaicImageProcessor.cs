using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CcCalendar.Core.Tools;

namespace CcCalendar.Desktop.Tools;

public static class MosaicImageProcessor
{
    public static BitmapSource Apply(BitmapSource source, CaptureRegion region, int blockSize = 12)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(blockSize, 2);
        BitmapSource converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        byte[] pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        int left = Math.Clamp(region.X, 0, converted.PixelWidth);
        int top = Math.Clamp(region.Y, 0, converted.PixelHeight);
        int right = Math.Clamp(region.X + region.Width, 0, converted.PixelWidth);
        int bottom = Math.Clamp(region.Y + region.Height, 0, converted.PixelHeight);

        for (int y = top; y < bottom; y += blockSize)
        {
            for (int x = left; x < right; x += blockSize)
            {
                int sample = y * stride + x * 4;
                int blockRight = Math.Min(x + blockSize, right);
                int blockBottom = Math.Min(y + blockSize, bottom);
                for (int targetY = y; targetY < blockBottom; targetY++)
                {
                    for (int targetX = x; targetX < blockRight; targetX++)
                    {
                        int target = targetY * stride + targetX * 4;
                        pixels[target] = pixels[sample];
                        pixels[target + 1] = pixels[sample + 1];
                        pixels[target + 2] = pixels[sample + 2];
                        pixels[target + 3] = pixels[sample + 3];
                    }
                }
            }
        }

        var result = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }
}
