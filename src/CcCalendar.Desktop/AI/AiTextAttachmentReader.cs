using System.IO;
using System.Text;

namespace CcCalendar.Desktop.AI;

public static class AiTextAttachmentReader
{
    public const int MaximumFileCount = 5;
    public const long MaximumFileBytes = 1024 * 1024;
    // P74-04：图片附件走视觉桥转写（不注入正文），单文件放宽到 4MB、
    // 总量放宽到 8MB；文本类仍按 1MB/2MB 旧限。
    public const long MaximumImageFileBytes = 4 * 1024 * 1024;
    public const long MaximumTotalBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions = new(
        [".txt", ".md", ".csv", ".json", ".ics", ".png", ".jpg", ".jpeg", ".webp", ".gif"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ImageExtensions = new(
        [".png", ".jpg", ".jpeg", ".webp", ".gif"],
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

            bool isImage = ImageExtensions.Contains(file.Extension);
            if (file.Length > (isImage ? MaximumImageFileBytes : MaximumFileBytes))
            {
                throw new InvalidDataException(
                    $"单个{(isImage ? "图片" : "文本")}附件不能超过 {(isImage ? MaximumImageFileBytes : MaximumFileBytes) / 1024 / 1024} MB。");
            }

            totalBytes += file.Length;
            if (totalBytes > MaximumTotalBytes)
            {
                throw new InvalidDataException($"附件合计不能超过 {MaximumTotalBytes / 1024 / 1024} MB。");
            }

            // P74-04：图片不解码文本——Content 留空，发送时由视觉桥转写注入；
            // base64 编码在转写器内按需进行，附件区不双份占内存。
            if (isImage)
            {
                attachments.Add(new AiTextAttachment(file.Name, fullPath, file.Length, string.Empty, IsImage: true));
                continue;
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
    string Content,
    bool IsImage = false);
