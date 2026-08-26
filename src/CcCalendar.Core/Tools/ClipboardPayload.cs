namespace CcCalendar.Core.Tools;

public sealed record ClipboardPayload(
    string? Text,
    string? ImagePath,
    IReadOnlyList<string> FilePaths);
