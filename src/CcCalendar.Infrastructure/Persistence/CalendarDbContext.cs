using CcCalendar.Core.Auditing;
using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using CcCalendar.Core.Recurrence;
using CcCalendar.Core.Reminders;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Sync;
using CcCalendar.Core.Todos;
using CcCalendar.Core.Tools;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Persistence;

public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TodoItem> Todos => Set<TodoItem>();

    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    public DbSet<TimeBlock> TimeBlocks => Set<TimeBlock>();

    public DbSet<RecurrenceSeries> RecurrenceSeries => Set<RecurrenceSeries>();

    public DbSet<WorkRecord> WorkRecords => Set<WorkRecord>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();

    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();

    public DbSet<ClipboardHistoryEntry> ClipboardHistoryEntries => Set<ClipboardHistoryEntry>();

    public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();

    public DbSet<SyncOutboxMessage> SyncOutboxMessages => Set<SyncOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly);
    }
}
