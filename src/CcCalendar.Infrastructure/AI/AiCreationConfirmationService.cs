using System.Globalization;
using CcCalendar.Core.AI;
using CcCalendar.Core.Auditing;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.AI;

public sealed class AiCreationConfirmationService : IAiCreationConfirmationService
{
    private readonly string connectionString;
    private readonly Dictionary<Guid, AiCreationDraft> pending = [];
    private readonly object pendingLock = new();
    private readonly TimeProvider timeProvider;

    public AiCreationConfirmationService(
        string databasePath,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        }.ToString();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public AiCreationPreview Preview(AiCreationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Guid proposalId = Guid.NewGuid();
        lock (pendingLock)
        {
            pending.Add(proposalId, draft);
        }

        return new AiCreationPreview(
            proposalId,
            draft.Kind,
            draft.Title,
            CreatePreviewFields(draft));
    }

    public async Task<AiCreationResult> ConfirmAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        AiCreationDraft draft;
        lock (pendingLock)
        {
            if (!pending.Remove(proposalId, out draft!))
            {
                throw new KeyNotFoundException("The creation proposal does not exist or was already handled.");
            }
        }

        try
        {
            object entity = CreateEntity(draft);
            Guid entityId = GetEntityId(entity);
            AuditEntry audit = AuditEntry.Record(
                entity.GetType().Name,
                entityId,
                AuditAction.AppliedAiChange,
                timeProvider.GetUtcNow(),
                canUndo: true);
            await using CalendarDbContext context = CreateContext();
            context.Add(entity);
            context.AuditEntries.Add(audit);
            await context.SaveChangesAsync(cancellationToken);
            return new AiCreationResult(entityId, draft.Kind);
        }
        catch
        {
            lock (pendingLock)
            {
                pending.TryAdd(proposalId, draft);
            }

            throw;
        }
    }

    public void Cancel(Guid proposalId)
    {
        lock (pendingLock)
        {
            pending.Remove(proposalId);
        }
    }

    private static List<AiPreviewField> CreatePreviewFields(AiCreationDraft draft)
    {
        return draft.Kind switch
        {
            AiCreationKind.Event when draft.IsAllDay =>
            [
                new("类型", "全天日程"),
                new("开始", draft.AllDayStart!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new("结束", draft.AllDayEndExclusive!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ],
            AiCreationKind.Event => CreateTimedPreviewFields(draft),
            AiCreationKind.Todo =>
            [
                new("类型", "待办"),
                new("截止", draft.DueAt?.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture) ?? "未设置"),
            ],
            AiCreationKind.Record =>
            [
                new("类型", draft.RecordType!.Value.ToString()),
                new("正文", draft.Content!),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(draft)),
        };
    }

    private static List<AiPreviewField> CreateTimedPreviewFields(AiCreationDraft draft)
    {
        var fields = new List<AiPreviewField>
        {
            new("类型", "定时日程"),
            new("开始", draft.StartAt!.Value.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)),
            new("结束", draft.EndAt!.Value.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)),
            new("时区", draft.TimeZoneId!),
        };
        if (!string.IsNullOrWhiteSpace(draft.Location))
        {
            fields.Add(new("地点", draft.Location!));
        }

        if (!string.IsNullOrWhiteSpace(draft.MeetingNumber))
        {
            fields.Add(new("腾讯会议号", draft.MeetingNumber!));
        }

        return fields;
    }

    private object CreateEntity(AiCreationDraft draft)
    {
        return draft.Kind switch
        {
            AiCreationKind.Event when draft.IsAllDay => CalendarEvent.CreateAllDay(
                draft.Title,
                null,
                draft.AllDayStart!.Value,
                draft.AllDayEndExclusive!.Value),
            AiCreationKind.Event => CalendarEvent.CreateTimed(
                draft.Title,
                null,
                draft.StartAt!.Value,
                draft.EndAt!.Value,
                draft.TimeZoneId!,
                draft.Location,
                CreateMeetingInvitationText(draft)),
            AiCreationKind.Todo => TodoItem.Create(draft.Title, null, draft.DueAt),
            AiCreationKind.Record => WorkRecord.Create(
                draft.RecordType!.Value,
                draft.Title,
                null,
                draft.Content!,
                timeProvider.GetUtcNow()),
            _ => throw new ArgumentOutOfRangeException(nameof(draft)),
        };
    }

    private static Guid GetEntityId(object entity)
    {
        return entity switch
        {
            CalendarEvent calendarEvent => calendarEvent.Id,
            TodoItem todo => todo.Id,
            WorkRecord record => record.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(entity)),
        };
    }

    private static string? CreateMeetingInvitationText(AiCreationDraft draft)
    {
        return string.IsNullOrWhiteSpace(draft.MeetingNumber)
            ? null
            : $"#腾讯会议：{draft.MeetingNumber}";
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
