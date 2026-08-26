using CcCalendar.Core.Reminders;
using CcCalendar.Infrastructure.Persistence;
using CcCalendar.Infrastructure.Reminders;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class ReminderDispatchServiceTests
{
    [Fact]
    public async Task DispatchQueuesOnlyDueReminderAndIsIdempotent()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Reminders.Add(Reminder.Create(
                ReminderTargetType.CalendarEvent,
                Guid.NewGuid(),
                "已错过的评审",
                now.AddMinutes(-15),
                now.AddHours(-1)));
            arrangeContext.Reminders.Add(Reminder.Create(
                ReminderTargetType.Todo,
                Guid.NewGuid(),
                "未来待办",
                now.AddMinutes(30),
                now.AddHours(-1)));
            await arrangeContext.SaveChangesAsync();
        }

        var service = new ReminderDispatchService(database.DatabasePath);

        int firstCount = await service.DispatchDueAsync(
            now,
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        int secondCount = await service.DispatchDueAsync(
            now,
            TimeSpan.FromMinutes(1),
            CancellationToken.None);

        await using CalendarDbContext assertContext = database.CreateContext();
        ReminderDelivery delivery = await assertContext.ReminderDeliveries.SingleAsync();
        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        Assert.True(delivery.WasMissed);
        Assert.Equal("已错过的评审", delivery.Title);
        Assert.Equal(1, await assertContext.Reminders.CountAsync(item => item.QueuedAtUtc != null));
    }

    [Fact]
    public async Task BackgroundSchedulerDispatchesAfterTriggerTimePasses()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var service = new ReminderDispatchService(database.DatabasePath);
        DateTimeOffset triggerAt = TimeProvider.System.GetUtcNow().AddMilliseconds(150);
        await service.ScheduleAsync(
            Reminder.Create(
                ReminderTargetType.Todo,
                Guid.NewGuid(),
                "后台触发",
                triggerAt,
                triggerAt.AddMinutes(-1)),
            CancellationToken.None);
        await using var scheduler = new ReminderBackgroundScheduler(
            service,
            TimeProvider.System,
            TimeSpan.FromMilliseconds(25),
            TimeSpan.FromSeconds(1));

        scheduler.Start();
        IReadOnlyList<ReminderDelivery> deliveries = [];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (deliveries.Count == 0)
        {
            await Task.Delay(25, timeout.Token);
            deliveries = await service.LoadPendingDeliveriesAsync(timeout.Token);
        }

        Assert.Single(deliveries);
        Assert.Equal("后台触发", deliveries[0].Title);
        Assert.False(deliveries[0].WasMissed);
    }

    [Fact]
    public async Task SnoozedDeliveryReappearsAtDueTimeAndAcknowledgementRemovesIt()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var service = new ReminderDispatchService(database.DatabasePath);
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        await service.ScheduleAsync(
            Reminder.Create(
                ReminderTargetType.Todo,
                Guid.NewGuid(),
                "延后处理",
                now,
                now.AddMinutes(-5)),
            CancellationToken.None);
        await service.DispatchDueAsync(now, TimeSpan.FromMinutes(1), CancellationToken.None);
        ReminderDelivery delivery = (await service.LoadReadyDeliveriesAsync(
            now,
            CancellationToken.None)).Single();

        await service.SnoozeAsync(
            delivery.Id,
            now,
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        Assert.Empty(await service.LoadReadyDeliveriesAsync(now.AddMinutes(9), CancellationToken.None));
        Assert.Single(await service.LoadReadyDeliveriesAsync(now.AddMinutes(10), CancellationToken.None));

        await service.AcknowledgeAsync(delivery.Id, now.AddMinutes(10), CancellationToken.None);

        Assert.Empty(await service.LoadReadyDeliveriesAsync(now.AddHours(1), CancellationToken.None));
    }
}
