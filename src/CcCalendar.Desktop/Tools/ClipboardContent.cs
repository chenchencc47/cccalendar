using CcCalendar.Core.Tools;

namespace CcCalendar.Desktop.Tools;

public sealed record ClipboardContent(
    ClipboardItemKind Kind,
    string? Text,
    byte[]? ImageBytes,
    IReadOnlyList<string> FilePaths)
{
    public static ClipboardContent FromText(string text) =>
        new(ClipboardItemKind.Text, text, null, []);

    public static ClipboardContent FromImage(byte[] imageBytes) =>
        new(ClipboardItemKind.Image, null, imageBytes, []);

    public static ClipboardContent FromFiles(IReadOnlyList<string> filePaths) =>
        new(ClipboardItemKind.Files, null, null, filePaths);
}
