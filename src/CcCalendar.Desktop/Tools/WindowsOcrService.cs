using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WindowsBitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using WpfBitmapFrame = System.Windows.Media.Imaging.BitmapFrame;

namespace CcCalendar.Desktop.Tools;

public static class WindowsOcrService
{
    public static async Task<string> RecognizeAsync(
        BitmapSource image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(WpfBitmapFrame.Create(image));
        using var memory = new MemoryStream();
        encoder.Save(memory);
        memory.Position = 0;
        using Windows.Storage.Streams.IRandomAccessStream randomAccess = memory.AsRandomAccessStream();
        WindowsBitmapDecoder decoder = await WindowsBitmapDecoder.CreateAsync(randomAccess);
        using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
        cancellationToken.ThrowIfCancellationRequested();
        OcrEngine engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException("当前系统语言不支持 Windows OCR。");
        OcrResult result = await engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Text;
    }
}
