using System.Data;
using CcCalendar.Core.Reminders;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Reminders;

public sealed class ReminderDispatchService : IReminderDispatcher, IReminderDeliveryStore
{
    private readonly string connectionString;

    public ReminderDispatchService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        };
        connectionString = builder.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string databasePath = new SqliteConnectionStringBuilder(connectionString).DataSource;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using CalendarDbContext context = CreateContext();
        await DatabaseInitializer.InitializeAsync(context, cancellationToken);
    }

    public async Task ScheduleAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        await using CalendarDbContext context = CreateContext();
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DispatchDueAsync(
        DateTimeOffset nowUtc,
        TimeSpan missedGracePeriod,
        CancellationToken cancellationToken)
    {
        DateTimeOffset normalizedNow = nowUtc.ToUniversalTime();
        await using CalendarDbContext context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        Reminder[] dueReminders = [.. await context.Reminders
            .Where(reminder => reminder.QueuedAtUtc == null && reminder.TriggerAtUtc <= normalizedNow)
            .OrderBy(reminder => reminder.TriggerAtUtc)
            .ToListAsync(cancellationToken)];

        foreach (Reminder reminder in dueReminders)
        {
            context.ReminderDeliveries.Add(reminder.Queue(normalizedNow, missedGracePeriod));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return dueReminders.Length;
    }

    public async Task<IReadOnlyList<ReminderDelivery>> LoadPendingDeliveriesAsync(
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        return await context.ReminderDeliveries
            .AsNoTracking()
            .Where(delivery => delivery.AcknowledgedAtUtc == null)
            .OrderBy(delivery => delivery.DispatchedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReminderDelivery>> LoadReadyDeliveriesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        DateTimeOffset normalizedNow = nowUtc.ToUniversalTime();
        await using CalendarDbContext context = CreateContext();
        return await context.ReminderDeliveries
            .AsNoTracking()
            .Where(delivery => delivery.AcknowledgedAtUtc == null
                && (delivery.SnoozedUntilUtc == null || delivery.SnoozedUntilUtc <= normalizedNow))
            .OrderBy(delivery => delivery.DispatchedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task SnoozeAsync(
        Guid deliveryId,
        DateTimeOffset nowUtc,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        ReminderDelivery? delivery = await context.ReminderDeliveries.SingleOrDefaultAsync(
            item => item.Id == deliveryId,
            cancellationToken);
        if (delivery is null)
        {
            return;
        }

        delivery.Snooze(nowUtc, duration);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AcknowledgeAsync(
        Guid deliveryId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        ReminderDelivery? delivery = await context.ReminderDeliveries.SingleOrDefaultAsync(
            item => item.Id == deliveryId,
            cancellationToken);
        if (delivery is null)
        {
            return;
        }

        delivery.Acknowledge(nowUtc);
        await context.SaveChangesAsync(cancellationToken);
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
