namespace CcCalendar.Core.Projects;

public sealed class Milestone
{
    private Milestone()
    {
        Title = null!;
    }

    internal Milestone(string title, DateOnly dueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = Guid.NewGuid();
        Title = title.Trim();
        DueDate = dueDate;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public DateOnly DueDate { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public bool IsCompleted => CompletedAtUtc.HasValue;

    public void Complete(DateTimeOffset completedAtUtc)
    {
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
    }
}
