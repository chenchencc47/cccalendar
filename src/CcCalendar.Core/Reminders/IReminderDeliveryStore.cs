namespace CcCalendar.Core.Reminders;

public interface IReminderDeliveryStore
{
    Task<IReadOnlyList<ReminderDelivery>> LoadReadyDeliveriesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task SnoozeAsync(
        Guid deliveryId,
        DateTimeOffset nowUtc,
        TimeSpan duration,
        CancellationToken cancellationToken);

    Task AcknowledgeAsync(
        Guid deliveryId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}
