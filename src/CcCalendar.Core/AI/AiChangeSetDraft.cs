using CcCalendar.Core.Todos;

namespace CcCalendar.Core.AI;

public enum AiEntityKind
{
    Project,
    Todo,
    Event,
    Record,
}

public abstract record AiChange;

public sealed record AiTodoStatusChange(Guid TodoId, TodoStatus NewStatus) : AiChange;

public sealed record AiRecycleChange(AiEntityKind EntityKind, Guid EntityId) : AiChange;

public sealed class AiChangeSetDraft
{
    public AiChangeSetDraft(string description, IReadOnlyList<AiChange> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
        {
            throw new ArgumentException("A change set must contain at least one change.", nameof(changes));
        }

        Description = description.Trim();
        Changes = changes;
    }

    public string Description { get; }

    public IReadOnlyList<AiChange> Changes { get; }
}

public sealed record AiChangePreview(
    Guid ProposalId,
    string Description,
    IReadOnlyList<string> Changes);

public sealed record AiChangeResult(int AppliedChangeCount);

public interface IAiChangeConfirmationService
{
    AiChangePreview Preview(AiChangeSetDraft draft);

    Task<AiChangeResult> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken);

    void Cancel(Guid proposalId);

    Task<string?> UndoLastAsync(CancellationToken cancellationToken);
}
