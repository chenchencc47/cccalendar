using CcCalendar.Core.Configuration;
using CcCalendar.Core.Reminders;

namespace CcCalendar.Desktop.Reminders;

public sealed class ReminderNotificationCoordinator
{
    private readonly IFullscreenDetector fullscreenDetector;
    private readonly HashSet<Guid> notifiedDeliveries = [];
    private readonly IReminderNotificationChannel popupChannel;
    private readonly IReminderDeliveryStore store;
    private readonly IReminderNotificationChannel soundChannel;
    private readonly IReminderNotificationChannel systemChannel;
    private readonly Func<ReminderNotificationSettings> settingsProvider;
    private readonly TimeProvider timeProvider;

    public ReminderNotificationCoordinator(
        IReminderDeliveryStore store,
        TimeProvider timeProvider,
        Func<ReminderNotificationSettings> settingsProvider,
        IFullscreenDetector fullscreenDetector,
        IReminderNotificationChannel systemChannel,
        IReminderNotificationChannel popupChannel,
        IReminderNotificationChannel soundChannel)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(fullscreenDetector);
        ArgumentNullException.ThrowIfNull(systemChannel);
        ArgumentNullException.ThrowIfNull(popupChannel);
        ArgumentNullException.ThrowIfNull(soundChannel);
        this.store = store;
        this.timeProvider = timeProvider;
        this.settingsProvider = settingsProvider;
        this.fullscreenDetector = fullscreenDetector;
        this.systemChannel = systemChannel;
        this.popupChannel = popupChannel;
        this.soundChannel = soundChannel;
    }

    public async Task<int> PollAsync(CancellationToken cancellationToken)
    {
        ReminderNotificationSettings settings = settingsProvider();
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();
        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(nowUtc, timeProvider.LocalTimeZone);
        if (DoNotDisturbPolicy.IsSuppressed(
            localNow,
            settings,
            fullscreenDetector.IsForegroundFullscreen()))
        {
            return 0;
        }

        if (!settings.IsSystemNotificationEnabled
            && !settings.IsPopupEnabled
            && !settings.IsSoundEnabled)
        {
            return 0;
        }

        IReadOnlyList<ReminderDelivery> ready = await store.LoadReadyDeliveriesAsync(
            nowUtc,
            cancellationToken);
        int notificationCount = 0;
        foreach (ReminderDelivery delivery in ready.Where(item => !notifiedDeliveries.Contains(item.Id)))
        {
            if (settings.IsPopupEnabled)
            {
                // Use the in-app notification as the primary visual surface. Showing
                // both visual channels for one delivery creates duplicate reminders.
                await popupChannel.ShowAsync(delivery, cancellationToken);
            }
            else if (settings.IsSystemNotificationEnabled)
            {
                await systemChannel.ShowAsync(delivery, cancellationToken);
            }

            if (settings.IsSoundEnabled)
            {
                await soundChannel.ShowAsync(delivery, cancellationToken);
            }

            notifiedDeliveries.Add(delivery.Id);
            notificationCount++;
        }

        return notificationCount;
    }

    public async Task SnoozeAsync(
        ReminderDelivery delivery,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await store.SnoozeAsync(
            delivery.Id,
            timeProvider.GetUtcNow(),
            duration,
            cancellationToken);
        notifiedDeliveries.Remove(delivery.Id);
    }

    public async Task AcknowledgeAsync(
        ReminderDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await store.AcknowledgeAsync(
            delivery.Id,
            timeProvider.GetUtcNow(),
            cancellationToken);
        notifiedDeliveries.Remove(delivery.Id);
    }
}
