namespace CcCalendar.Core.Tools;

public interface IClipboardHistoryService
{
    Task<ClipboardHistoryEntry> AddTextAsync(string text, CancellationToken cancellationToken);

    Task<ClipboardHistoryEntry> AddImageAsync(byte[] imageBytes, CancellationToken cancellationToken);

    Task<ClipboardHistoryEntry> AddFilesAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardHistoryEntry>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken);

    Task<ClipboardPayload> GetPayloadAsync(Guid id, CancellationToken cancellationToken);
}
