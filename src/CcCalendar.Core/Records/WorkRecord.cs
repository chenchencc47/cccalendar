using CcCalendar.Core.Recycling;

namespace CcCalendar.Core.Records;

public sealed class WorkRecord : IRecyclableEntity
{
    private readonly List<RecordVersion> versions = [];
    private readonly List<RecordAttachment> attachments = [];

    private WorkRecord()
    {
        Title = null!;
    }

    private WorkRecord(
        WorkRecordType type,
        string title,
        Guid? projectId,
        string initialContent,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(initialContent);

        Id = Guid.NewGuid();
        Type = type;
        Title = title.Trim();
        ProjectId = projectId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        versions.Add(new RecordVersion(1, initialContent, CreatedAtUtc));
    }

    public Guid Id { get; private set; }

    public WorkRecordType Type { get; private set; }

    public string Title { get; private set; }

    public Guid? ProjectId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public IReadOnlyList<RecordVersion> Versions => versions;

    public IReadOnlyList<RecordAttachment> Attachments => attachments;

    public string CurrentContent => versions.MaxBy(version => version.VersionNumber)!.Content;

    public static WorkRecord Create(
        WorkRecordType type,
        string title,
        Guid? projectId,
        string initialContent,
        DateTimeOffset createdAtUtc)
    {
        return new WorkRecord(type, title, projectId, initialContent, createdAtUtc);
    }

    public bool SaveRevision(string content, DateTimeOffset savedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.Equals(CurrentContent, content, StringComparison.Ordinal))
        {
            return false;
        }

        AddVersion(content, savedAtUtc);
        return true;
    }

    public void RestoreVersion(Guid versionId, DateTimeOffset restoredAtUtc)
    {
        RecordVersion version = versions.SingleOrDefault(candidate => candidate.Id == versionId)
            ?? throw new KeyNotFoundException("The record version does not exist.");
        AddVersion(version.Content, restoredAtUtc);
    }

    public RecordAttachment AddAttachment(
        string fileName,
        string relativePath,
        string mediaType,
        long sizeBytes)
    {
        var attachment = new RecordAttachment(fileName, relativePath, mediaType, sizeBytes);
        attachments.Add(attachment);
        return attachment;
    }

    public void MoveToRecycleBin(DateTimeOffset deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc.ToUniversalTime();
    }

    public void RestoreFromRecycleBin()
    {
        DeletedAtUtc = null;
    }

    private void AddVersion(string content, DateTimeOffset savedAtUtc)
    {
        var version = new RecordVersion(versions.Count + 1, content, savedAtUtc);
        versions.Add(version);
        UpdatedAtUtc = version.SavedAtUtc;
    }
}
