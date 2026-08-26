namespace CcCalendar.Core.Tools;

public sealed class ClipboardHistoryEntry
{
    private ClipboardHistoryEntry()
    {
    }

    private ClipboardHistoryEntry(
        ClipboardItemKind kind,
        string preview,
        string searchText,
        string? textContent,
        string? imageRelativePath,
        string? filePaths,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        Preview = preview;
        SearchText = searchText;
        TextContent = textContent;
        ImageRelativePath = imageRelativePath;
        FilePaths = filePaths;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public ClipboardItemKind Kind { get; private set; }

    public string Preview { get; private set; } = string.Empty;

    public string SearchText { get; private set; } = string.Empty;

    public string? TextContent { get; private set; }

    public string? ImageRelativePath { get; private set; }

    public string? FilePaths { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsFavorite { get; private set; }

    public static ClipboardHistoryEntry CreateText(string text, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string normalized = text.Trim();
        return new ClipboardHistoryEntry(
            ClipboardItemKind.Text,
            CreatePreview(normalized),
            normalized.ToUpperInvariant(),
            normalized,
            null,
            null,
            createdAtUtc);
    }

    public static ClipboardHistoryEntry CreateImage(
        string imageRelativePath,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageRelativePath);
        return new ClipboardHistoryEntry(
            ClipboardItemKind.Image,
            "图片",
            string.Empty,
            null,
            imageRelativePath,
            null,
            createdAtUtc);
    }

    public static ClipboardHistoryEntry CreateFiles(
        IReadOnlyList<string> filePaths,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count == 0 || filePaths.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one file path is required.", nameof(filePaths));
        }

        string[] paths = [.. filePaths];
        string preview = string.Join(", ", paths.Select(Path.GetFileName));
        return new ClipboardHistoryEntry(
            ClipboardItemKind.Files,
            CreatePreview(preview),
            string.Join('\n', paths).ToUpperInvariant(),
            null,
            null,
            string.Join('\n', paths),
            createdAtUtc);
    }

    public void ToggleFavorite() => IsFavorite = !IsFavorite;

    private static string CreatePreview(string value) =>
        value.Length <= 120 ? value : value[..120];
}
