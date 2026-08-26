namespace CcCalendar.Core.Records;

public sealed class RecordAttachment
{
    private RecordAttachment()
    {
        FileName = null!;
        RelativePath = null!;
        MediaType = null!;
    }

    internal RecordAttachment(string fileName, string relativePath, string mediaType, long sizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);

        string normalizedPath = relativePath.Replace('\\', '/');
        string[] pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(relativePath)
            || pathSegments.Length == 0
            || pathSegments.Any(segment => segment is "." or "..")
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The attachment path must be a safe relative path.", nameof(relativePath));
        }

        Id = Guid.NewGuid();
        FileName = fileName.Trim();
        RelativePath = string.Join('/', pathSegments);
        MediaType = mediaType.Trim();
        SizeBytes = sizeBytes;
    }

    public Guid Id { get; private set; }

    public string FileName { get; private set; }

    public string RelativePath { get; private set; }

    public string MediaType { get; private set; }

    public long SizeBytes { get; private set; }
}
