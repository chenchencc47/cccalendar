using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class SyncPersistenceTests
{
    [Fact]
    public async Task MetadataAndOutboxRoundTripAfterReopeningContext()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid deviceId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);

        await using (CalendarDbContext context = database.CreateContext())
        {
            context.SyncMetadata.Add(SyncMetadata.Create(deviceId, now));
            context.SyncOutboxMessages.Add(SyncOutboxMessage.Enqueue(
                "CalendarEvent",
                eventId,
                SyncOperationKind.Created,
                "{}",
                now));
            await context.SaveChangesAsync();
        }

        await using (CalendarDbContext context = database.CreateContext())
        {
            SyncMetadata metadata = Assert.Single(await context.SyncMetadata.ToListAsync());
            SyncOutboxMessage message = Assert.Single(await context.SyncOutboxMessages.ToListAsync());

            Assert.Equal(deviceId, metadata.DeviceId);
            Assert.Equal(eventId, message.EntityId);
            Assert.Equal(SyncOperationKind.Created, message.Operation);
        }
    }
}
