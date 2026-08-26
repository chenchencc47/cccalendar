using CcCalendar.Core.AI;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Infrastructure.AI;
using CcCalendar.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class AiCreationConfirmationServiceTests
{
    [Fact]
    public async Task PreviewDoesNotWriteAndConfirmCreatesEachSupportedKindWithAudit()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        var service = new AiCreationConfirmationService(
            database.DatabasePath,
            new FixedTimeProvider(now));
        AiCreationPreview eventPreview = service.Preview(AiCreationDraft.TimedEvent(
            "Design review",
            new DateTimeOffset(2026, 8, 17, 2, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 17, 3, 0, 0, TimeSpan.Zero),
            "China Standard Time"));
        AiCreationPreview todoPreview = service.Preview(AiCreationDraft.Todo(
            "Prepare release",
            new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero)));
        AiCreationPreview recordPreview = service.Preview(AiCreationDraft.Record(
            "Meeting notes",
            WorkRecordType.Meeting,
            "Confirmed decisions"));

        await using (var before = database.CreateContext())
        {
            Assert.Empty(await before.CalendarEvents.ToListAsync());
            Assert.Empty(await before.Todos.ToListAsync());
            Assert.Empty(await before.WorkRecords.ToListAsync());
            Assert.Empty(await before.AuditEntries.ToListAsync());
        }

        await service.ConfirmAsync(eventPreview.ProposalId, CancellationToken.None);
        await service.ConfirmAsync(todoPreview.ProposalId, CancellationToken.None);
        await service.ConfirmAsync(recordPreview.ProposalId, CancellationToken.None);

        await using var after = database.CreateContext();
        CalendarEvent createdEvent = Assert.Single(await after.CalendarEvents.ToListAsync());
        Assert.Null(createdEvent.Location);
        Assert.Null(createdEvent.MeetingInvitationText);
        Assert.Single(await after.Todos.ToListAsync());
        Assert.Single(await after.WorkRecords.ToListAsync());
        Assert.Equal(3, await after.AuditEntries.CountAsync());
        Assert.All(await after.AuditEntries.ToListAsync(), entry =>
            Assert.Equal(CcCalendar.Core.Auditing.AuditAction.AppliedAiChange, entry.Action));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ConfirmAsync(todoPreview.ProposalId, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmTimedEventPersistsMeetingLocationAndNumberSeparately()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var service = new AiCreationConfirmationService(database.DatabasePath);
        AiCreationPreview preview = service.Preview(AiCreationDraft.TimedEvent(
            "每日例会",
            new DateTimeOffset(2026, 8, 24, 10, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 11, 10, 0, TimeSpan.Zero),
            "China Standard Time",
            "项目组二楼会议室",
            "365-5683-5623"));

        await service.ConfirmAsync(preview.ProposalId, CancellationToken.None);

        await using var context = database.CreateContext();
        CalendarEvent created = Assert.Single(await context.CalendarEvents.ToListAsync());
        Assert.Equal("每日例会", created.Title);
        Assert.Equal("项目组二楼会议室", created.Location);
        Assert.Equal("#腾讯会议：365-5683-5623", created.MeetingInvitationText);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
