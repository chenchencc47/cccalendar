using CcCalendar.Core.Auditing;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class AuditPersistenceTests
{
    [Fact]
    public async Task AuditEntryRoundTripsWithoutContentPayload()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid auditId;

        await using (var writeContext = database.CreateContext())
        {
            AuditEntry entry = AuditEntry.Record(
                "TodoItem",
                Guid.NewGuid(),
                AuditAction.Updated,
                new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero),
                canUndo: true);
            auditId = entry.Id;
            writeContext.AuditEntries.Add(entry);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        AuditEntry actual = await readContext.AuditEntries.SingleAsync(entry => entry.Id == auditId);

        Assert.Equal("TodoItem", actual.EntityType);
        Assert.True(actual.CanUndo);
    }
}
