namespace CcCalendar.Infrastructure.Reminders;

public interface IReminderDispatcher
{
    Task<int> DispatchDueAsync(
        DateTimeOffset nowUtc,
        TimeSpan missedGracePeriod,
        CancellationToken cancellationToken);
}
