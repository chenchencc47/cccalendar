using System.IO;
using System.Text;

namespace CcCalendar.Desktop.AI;

public static class AiTextAttachmentReader
{
    public const int MaximumFileCount = 5;
    public const long MaximumFileBytes = 1024 * 1024;
    public const long MaximumTotalBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions = new(
        [".txt", ".md", ".csv", ".json", ".ics"],
        StringComparer.OrdinalIgnoreCase);

    public static async Task<IReadOnlyList<AiTextAttachment>> ReadAsync(
        IEnumerable<string> filePaths,
        int existingFileCount,
        long existingBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        string[] paths = [.. filePaths];
        if (existingFileCount + paths.Length > MaximumFileCount)
        {
            throw new InvalidDataException($"最多可附加 {MaximumFileCount} 个文件。");
        }

        var attachments = new List<AiTextAttachment>(paths.Length);
        long totalBytes = existingBytes;
        foreach (string path in paths)
        {
            string fullPath = Path.GetFullPath(path);
            var file = new FileInfo(fullPath);
            if (!SupportedExtensions.Contains(file.Extension))
            {
                throw new InvalidDataException($"不支持 {file.Extension} 文件。");
            }

            if (!file.Exists)
            {
                throw new FileNotFoundException("找不到附件文件。", fullPath);
            }

            if (file.Length > MaximumFileBytes)
            {
                throw new InvalidDataException($"单个附件不能超过 {MaximumFileBytes / 1024 / 1024} MB。");
            }

            totalBytes += file.Length;
            if (totalBytes > MaximumTotalBytes)
            {
                throw new InvalidDataException($"附件合计不能超过 {MaximumTotalBytes / 1024 / 1024} MB。");
            }

            using var reader = new StreamReader(
                fullPath,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            string content = await reader.ReadToEndAsync(cancellationToken);
            attachments.Add(new AiTextAttachment(file.Name, fullPath, file.Length, content));
        }

        return attachments;
    }
}

public sealed record AiTextAttachment(
    string FileName,
    string FilePath,
    long SizeBytes,
    string Content);
