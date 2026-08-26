using CcCalendar.Core.Records;

namespace CcCalendar.Core.AI;

public enum AiCreationKind
{
    Event,
    Todo,
    Record,
}

public sealed class AiCreationDraft
{
    private AiCreationDraft(AiCreationKind kind, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Kind = kind;
        Title = title.Trim();
    }

    public AiCreationKind Kind { get; }

    public string Title { get; }

    public bool IsAllDay { get; private init; }

    public DateTimeOffset? StartAt { get; private init; }

    public DateTimeOffset? EndAt { get; private init; }

    public string? TimeZoneId { get; private init; }

    public string? Location { get; private init; }

    public string? MeetingNumber { get; private init; }

    public DateOnly? AllDayStart { get; private init; }

    public DateOnly? AllDayEndExclusive { get; private init; }

    public DateTimeOffset? DueAt { get; private init; }

    public WorkRecordType? RecordType { get; private init; }

    public string? Content { get; private init; }

    public static AiCreationDraft TimedEvent(
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string timeZoneId,
        string? location = null,
        string? meetingNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (endAt <= startAt)
        {
            throw new ArgumentException("The end time must be after the start time.", nameof(endAt));
        }

        return new AiCreationDraft(AiCreationKind.Event, title)
        {
            StartAt = startAt,
            EndAt = endAt,
            TimeZoneId = timeZoneId.Trim(),
            Location = NormalizeOptional(location),
            MeetingNumber = NormalizeOptional(meetingNumber),
        };
    }

    public static AiCreationDraft AllDayEvent(
        string title,
        DateOnly startDate,
        DateOnly endDateExclusive)
    {
        if (endDateExclusive <= startDate)
        {
            throw new ArgumentException(
                "The exclusive end date must be after the start date.",
                nameof(endDateExclusive));
        }

        return new AiCreationDraft(AiCreationKind.Event, title)
        {
            IsAllDay = true,
            AllDayStart = startDate,
            AllDayEndExclusive = endDateExclusive,
        };
    }

    public static AiCreationDraft Todo(string title, DateTimeOffset? dueAt)
    {
        return new AiCreationDraft(AiCreationKind.Todo, title) { DueAt = dueAt };
    }

    public static AiCreationDraft Record(
        string title,
        WorkRecordType recordType,
        string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new AiCreationDraft(AiCreationKind.Record, title)
        {
            RecordType = recordType,
            Content = content,
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? null : trimmed;
    }
}

public sealed record AiPreviewField(string Label, string Value);

public sealed record AiCreationPreview(
    Guid ProposalId,
    AiCreationKind Kind,
    string Title,
    IReadOnlyList<AiPreviewField> Fields);

public sealed record AiCreationResult(Guid EntityId, AiCreationKind Kind);

public interface IAiCreationConfirmationService
{
    AiCreationPreview Preview(AiCreationDraft draft);

    Task<AiCreationResult> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken);

    void Cancel(Guid proposalId);
}
