namespace CcCalendar.Core.Records;

public sealed class RecordVersion
{
    private RecordVersion()
    {
        Content = null!;
    }

    internal RecordVersion(int versionNumber, string content, DateTimeOffset savedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);

        Id = Guid.NewGuid();
        VersionNumber = versionNumber;
        Content = content;
        SavedAtUtc = savedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public int VersionNumber { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset SavedAtUtc { get; private set; }
}
