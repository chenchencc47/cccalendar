using CcCalendar.Core.Configuration;
using CcCalendar.Core.Reminders;
using CcCalendar.Desktop.Reminders;

namespace CcCalendar.Desktop.Tests.Reminders;

public sealed class ReminderNotificationCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PollNotifiesEnabledChannelsOnlyOncePerDelivery()
    {
        ReminderDelivery delivery = Reminder.Create(
            ReminderTargetType.Todo,
            Guid.NewGuid(),
            "提交周报",
            Now,
            Now.AddMinutes(-5)).Queue(Now, TimeSpan.FromMinutes(1));
        var store = new FakeDeliveryStore(delivery);
        var systemChannel = new RecordingChannel();
        var popupChannel = new RecordingChannel();
        var soundChannel = new RecordingChannel();
        var coordinator = new ReminderNotificationCoordinator(
            store,
            new FixedTimeProvider(Now),
            () => new ReminderNotificationSettings
            {
                IsSystemNotificationEnabled = true,
                IsPopupEnabled = false,
                IsSoundEnabled = true,
            },
            new NeverFullscreenDetector(),
            systemChannel,
            popupChannel,
            soundChannel);

        int firstCount = await coordinator.PollAsync(CancellationToken.None);
        int secondCount = await coordinator.PollAsync(CancellationToken.None);

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        Assert.Single(systemChannel.Deliveries);
        Assert.Empty(popupChannel.Deliveries);
        Assert.Single(soundChannel.Deliveries);
    }

    [Fact]
    public async Task PopupTakesPrecedenceOverSystemNotificationForOneDelivery()
    {
        ReminderDelivery delivery = Reminder.Create(
            ReminderTargetType.CalendarEvent,
            Guid.NewGuid(),
            "会议开始",
            Now,
            Now.AddMinutes(-5)).Queue(Now, TimeSpan.FromMinutes(1));
        var store = new FakeDeliveryStore(delivery);
        var systemChannel = new RecordingChannel();
        var popupChannel = new RecordingChannel();
        var soundChannel = new RecordingChannel();
        var coordinator = new ReminderNotificationCoordinator(
            store,
            new FixedTimeProvider(Now),
            () => new ReminderNotificationSettings
            {
                IsSystemNotificationEnabled = true,
                IsPopupEnabled = true,
                IsSoundEnabled = true,
            },
            new NeverFullscreenDetector(),
            systemChannel,
            popupChannel,
            soundChannel);

        int count = await coordinator.PollAsync(CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Empty(systemChannel.Deliveries);
        Assert.Single(popupChannel.Deliveries);
        Assert.Single(soundChannel.Deliveries);
    }

    [Fact]
    public async Task QuietHoursDoNotLoadOrConsumeReadyDeliveries()
    {
        ReminderDelivery delivery = Reminder.Create(
            ReminderTargetType.Todo,
            Guid.NewGuid(),
            "勿扰期间保留",
            Now,
            Now.AddMinutes(-5)).Queue(Now, TimeSpan.FromMinutes(1));
        var store = new FakeDeliveryStore(delivery);
        var channel = new RecordingChannel();
        var coordinator = new ReminderNotificationCoordinator(
            store,
            new FixedTimeProvider(Now),
            () => new ReminderNotificationSettings
            {
                IsDoNotDisturbEnabled = true,
                DoNotDisturbStart = new TimeOnly(7, 0),
                DoNotDisturbEnd = new TimeOnly(9, 0),
            },
            new NeverFullscreenDetector(),
            channel,
            channel,
            channel);

        int count = await coordinator.PollAsync(CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Equal(0, store.LoadCount);
        Assert.Empty(channel.Deliveries);
    }

    private sealed class FakeDeliveryStore(ReminderDelivery delivery) : IReminderDeliveryStore
    {
        public int LoadCount { get; private set; }

        public Task<IReadOnlyList<ReminderDelivery>> LoadReadyDeliveriesAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            return Task.FromResult<IReadOnlyList<ReminderDelivery>>([delivery]);
        }

        public Task SnoozeAsync(Guid deliveryId, DateTimeOffset nowUtc, TimeSpan duration, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AcknowledgeAsync(Guid deliveryId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingChannel : IReminderNotificationChannel
    {
        public List<ReminderDelivery> Deliveries { get; } = [];

        public Task ShowAsync(ReminderDelivery delivery, CancellationToken cancellationToken)
        {
            Deliveries.Add(delivery);
            return Task.CompletedTask;
        }
    }

    private sealed class NeverFullscreenDetector : IFullscreenDetector
    {
        public bool IsForegroundFullscreen() => false;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
