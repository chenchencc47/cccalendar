using CcCalendar.Core.Records;

namespace CcCalendar.Core.Tests.Records;

public sealed class WorkRecordTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateAddsInitialVersion()
    {
        WorkRecord record = WorkRecord.Create(
            WorkRecordType.Meeting,
            "  Planning review  ",
            null,
            "Initial notes",
            CreatedAtUtc);

        Assert.Equal("Planning review", record.Title);
        Assert.Equal("Initial notes", record.CurrentContent);
        Assert.Single(record.Versions);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void SaveRevisionSkipsUnchangedContentAndAppendsChangedContent()
    {
        WorkRecord record = CreateRecord();

        bool unchangedSaved = record.SaveRevision("Initial notes", CreatedAtUtc.AddMinutes(1));
        bool changedSaved = record.SaveRevision("Updated notes", CreatedAtUtc.AddMinutes(2));

        Assert.False(unchangedSaved);
        Assert.True(changedSaved);
        Assert.Equal(2, record.Versions.Count);
        Assert.Equal("Updated notes", record.CurrentContent);
        Assert.Equal(CreatedAtUtc.AddMinutes(2), record.UpdatedAtUtc);
    }

    [Fact]
    public void RestoreAppendsCopyOfHistoricalContent()
    {
        WorkRecord record = CreateRecord();
        Guid firstVersionId = record.Versions[0].Id;
        record.SaveRevision("Updated notes", CreatedAtUtc.AddMinutes(2));

        record.RestoreVersion(firstVersionId, CreatedAtUtc.AddMinutes(3));

        Assert.Equal(3, record.Versions.Count);
        Assert.Equal("Initial notes", record.CurrentContent);
    }

    [Fact]
    public void AddAttachmentStoresOnlySafeRelativeMetadata()
    {
        WorkRecord record = CreateRecord();

        RecordAttachment attachment = record.AddAttachment(
            "design.png",
            "records/2026/design.png",
            "image/png",
            1024);

        Assert.Single(record.Attachments, attachment);
        Assert.Throws<ArgumentException>(() => record.AddAttachment(
            "secret.txt",
            "../secret.txt",
            "text/plain",
            10));
    }

    private static WorkRecord CreateRecord()
    {
        return WorkRecord.Create(
            WorkRecordType.WorkLog,
            "Daily log",
            null,
            "Initial notes",
            CreatedAtUtc);
    }
}
