using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CcCalendar.Desktop.Tools;

public sealed class WindowsClipboardContentReader : IClipboardContentReader
{
    public ClipboardContent? Read()
    {
        if (Clipboard.ContainsFileDropList())
        {
            return ClipboardContent.FromFiles([.. Clipboard.GetFileDropList().Cast<string>()]);
        }

        if (Clipboard.ContainsImage())
        {
            BitmapSource? image = Clipboard.GetImage();
            if (image is null)
            {
                return null;
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return ClipboardContent.FromImage(stream.ToArray());
        }

        if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
        {
            string text = Clipboard.GetText(TextDataFormat.UnicodeText);
            return string.IsNullOrWhiteSpace(text) ? null : ClipboardContent.FromText(text);
        }

        return null;
    }
}
