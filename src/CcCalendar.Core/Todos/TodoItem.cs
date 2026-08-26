using CcCalendar.Core.Recycling;

namespace CcCalendar.Core.Todos;

public sealed class TodoItem : IRecyclableEntity
{
    private readonly List<TodoSubtask> subtasks = [];
    private readonly List<TodoDependency> dependencies = [];

    private TodoItem()
    {
        Title = null!;
    }

    private TodoItem(string title, Guid? projectId, DateTimeOffset? dueAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = Guid.NewGuid();
        Title = title.Trim();
        ProjectId = projectId;
        DueAtUtc = dueAtUtc?.ToUniversalTime();
        Status = TodoStatus.Inbox;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public Guid? ProjectId { get; private set; }

    public DateTimeOffset? DueAtUtc { get; private set; }

    public DateTimeOffset? StartAtUtc { get; private set; }

    public TodoStatus Status { get; private set; }

    public bool IsImportant { get; private set; }

    public bool? IsUrgentOverride { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public IReadOnlyList<TodoSubtask> Subtasks => subtasks;

    public IReadOnlyList<TodoDependency> Dependencies => dependencies;

    public int ProgressPercent => Status == TodoStatus.Completed
        ? 100
        : subtasks.Count == 0
            ? 0
            : subtasks.Count(subtask => subtask.IsCompleted) * 100 / subtasks.Count;

    public static TodoItem Create(string title, Guid? projectId, DateTimeOffset? dueAtUtc)
    {
        return new TodoItem(title, projectId, dueAtUtc);
    }

    public TodoQuadrant GetQuadrant(DateTimeOffset nowUtc, TimeSpan urgentThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(urgentThreshold, TimeSpan.Zero);

        bool isUrgent = IsUrgentOverride
            ?? (DueAtUtc.HasValue && DueAtUtc <= nowUtc.ToUniversalTime().Add(urgentThreshold));

        return (IsImportant, isUrgent) switch
        {
            (true, true) => TodoQuadrant.ImportantUrgent,
            (true, false) => TodoQuadrant.ImportantNotUrgent,
            (false, true) => TodoQuadrant.NotImportantUrgent,
            _ => TodoQuadrant.NotImportantNotUrgent,
        };
    }

    public void MoveToQuadrant(TodoQuadrant quadrant)
    {
        (IsImportant, IsUrgentOverride) = quadrant switch
        {
            TodoQuadrant.ImportantUrgent => (true, true),
            TodoQuadrant.ImportantNotUrgent => (true, false),
            TodoQuadrant.NotImportantUrgent => (false, true),
            TodoQuadrant.NotImportantNotUrgent => (false, false),
            _ => throw new ArgumentOutOfRangeException(nameof(quadrant)),
        };
    }

    public TodoSubtask AddSubtask(string title)
    {
        var subtask = new TodoSubtask(title);
        subtasks.Add(subtask);
        return subtask;
    }

    public void SetPlanningWindow(DateTimeOffset startAtUtc, DateTimeOffset dueAtUtc)
    {
        DateTimeOffset normalizedStart = startAtUtc.ToUniversalTime();
        DateTimeOffset normalizedDue = dueAtUtc.ToUniversalTime();

        if (normalizedDue < normalizedStart)
        {
            throw new ArgumentException("The due time cannot be before the start time.", nameof(dueAtUtc));
        }

        StartAtUtc = normalizedStart;
        DueAtUtc = normalizedDue;
    }

    public TodoDependency AddDependency(Guid dependsOnTodoItemId)
    {
        if (dependsOnTodoItemId == Guid.Empty || dependsOnTodoItemId == Id)
        {
            throw new ArgumentException("A todo cannot depend on itself or an empty identifier.", nameof(dependsOnTodoItemId));
        }

        if (dependencies.Any(dependency => dependency.DependsOnTodoItemId == dependsOnTodoItemId))
        {
            throw new InvalidOperationException("The todo dependency already exists.");
        }

        var dependency = new TodoDependency(dependsOnTodoItemId);
        dependencies.Add(dependency);
        return dependency;
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        Status = TodoStatus.Completed;
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
    }

    public void MoveToStatus(TodoStatus status)
    {
        if (status == TodoStatus.Completed)
        {
            throw new InvalidOperationException("Use Complete to record the completion timestamp.");
        }

        Status = status;
        CompletedAtUtc = null;
    }

    public void Reopen()
    {
        Status = TodoStatus.NotStarted;
        CompletedAtUtc = null;
    }

    public void MoveToRecycleBin(DateTimeOffset deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc.ToUniversalTime();
    }

    public void RestoreFromRecycleBin()
    {
        DeletedAtUtc = null;
    }
}
