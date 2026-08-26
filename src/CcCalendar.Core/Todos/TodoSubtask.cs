namespace CcCalendar.Core.Todos;

public sealed class TodoSubtask
{
    private TodoSubtask()
    {
        Title = null!;
    }

    internal TodoSubtask(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = Guid.NewGuid();
        Title = title.Trim();
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public bool IsCompleted => CompletedAtUtc.HasValue;

    public void Complete(DateTimeOffset completedAtUtc)
    {
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
    }
}
