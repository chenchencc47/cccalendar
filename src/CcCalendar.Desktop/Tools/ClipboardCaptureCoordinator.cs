using CcCalendar.Core.Tools;

namespace CcCalendar.Desktop.Tools;

public sealed class ClipboardCaptureCoordinator(
    IClipboardContentReader reader,
    IClipboardHistoryService historyService)
{
    public event EventHandler? Captured;

    public async Task CaptureCurrentAsync(CancellationToken cancellationToken)
    {
        ClipboardContent? content = reader.Read();
        if (content is null)
        {
            return;
        }

        switch (content.Kind)
        {
            case ClipboardItemKind.Text when content.Text is not null:
                await historyService.AddTextAsync(content.Text, cancellationToken);
                break;
            case ClipboardItemKind.Image when content.ImageBytes is not null:
                await historyService.AddImageAsync(content.ImageBytes, cancellationToken);
                break;
            case ClipboardItemKind.Files when content.FilePaths.Count > 0:
                await historyService.AddFilesAsync(content.FilePaths, cancellationToken);
                break;
            default:
                return;
        }

        Captured?.Invoke(this, EventArgs.Empty);
    }
}
