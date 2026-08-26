namespace CcCalendar.Core.AI;

public enum AiConflictChoice
{
    KeepOverlap,
    FindAvailableTime,
    Modify,
}

public sealed record AiConflictPrompt(
    bool HasConflict,
    int ConflictCount,
    IReadOnlyList<AiConflictChoice> Choices);

public sealed record AiAvailableSlot(
    DateOnly Date,
    TimeOnly LocalStart,
    TimeOnly LocalEnd,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc);

public sealed class AiTaskDecompositionDraft
{
    public AiTaskDecompositionDraft(Guid todoId, IReadOnlyList<string> subtaskTitles)
    {
        if (todoId == Guid.Empty)
        {
            throw new ArgumentException("A decomposition must reference a todo.", nameof(todoId));
        }

        ArgumentNullException.ThrowIfNull(subtaskTitles);
        string[] normalized = [.. subtaskTitles
            .Select(title => title?.Trim())
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A decomposition must contain subtasks.", nameof(subtaskTitles));
        }

        TodoId = todoId;
        SubtaskTitles = normalized;
    }

    public Guid TodoId { get; }

    public IReadOnlyList<string> SubtaskTitles { get; }
}

public sealed record AiTaskDecompositionPreview(
    Guid ProposalId,
    Guid TodoId,
    IReadOnlyList<string> SubtaskTitles);

public interface IAiPlanningService
{
    Task<AiConflictPrompt> CheckConflictAsync(
        AiCreationDraft candidate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiAvailableSlot>> FindAvailableSlotsAsync(
        DateOnly calendarDate,
        TimeSpan duration,
        string timeZoneId,
        CancellationToken cancellationToken);

    AiTaskDecompositionPreview PreviewTaskDecomposition(AiTaskDecompositionDraft draft);

    Task ConfirmTaskDecompositionAsync(Guid proposalId, CancellationToken cancellationToken);

    void CancelTaskDecomposition(Guid proposalId);
}
