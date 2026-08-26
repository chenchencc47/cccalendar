using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class WorkRecordPersistenceTests
{
    [Fact]
    public async Task WorkRecordRoundTripsWithVersionsAndAttachments()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var createdAtUtc = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        Guid recordId;

        await using (var writeContext = database.CreateContext())
        {
            Project project = Project.Create("cccalendar", "#246BCE", null, null);
            WorkRecord record = WorkRecord.Create(
                WorkRecordType.RequirementChange,
                "Calendar requirements",
                project.Id,
                "Initial",
                createdAtUtc);
            record.SaveRevision("Updated", createdAtUtc.AddMinutes(1));
            record.AddAttachment("design.png", "records/design.png", "image/png", 1024);
            recordId = record.Id;

            writeContext.AddRange(project, record);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        WorkRecord actual = await readContext.WorkRecords
            .Include(record => record.Versions)
            .Include(record => record.Attachments)
            .SingleAsync(record => record.Id == recordId);

        Assert.Equal("Updated", actual.CurrentContent);
        Assert.Equal(2, actual.Versions.Count);
        Assert.Single(actual.Attachments);
    }
}
