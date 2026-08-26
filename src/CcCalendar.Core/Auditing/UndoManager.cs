namespace CcCalendar.Core.Auditing;

public sealed class UndoManager
{
    private readonly Stack<UndoOperation> operations = [];

    public void Register(string description, Func<CancellationToken, Task> undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(undo);
        operations.Push(new UndoOperation(description.Trim(), undo));
    }

    public async Task<string?> UndoLastAsync(CancellationToken cancellationToken)
    {
        if (operations.Count == 0)
        {
            return null;
        }

        UndoOperation operation = operations.Peek();
        await operation.Undo(cancellationToken);
        operations.Pop();
        return operation.Description;
    }

    private sealed record UndoOperation(
        string Description,
        Func<CancellationToken, Task> Undo);
}
