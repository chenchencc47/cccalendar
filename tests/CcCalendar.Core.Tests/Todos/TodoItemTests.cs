using CcCalendar.Core.Todos;

namespace CcCalendar.Core.Tests.Todos;

public sealed class TodoItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan UrgentThreshold = TimeSpan.FromHours(24);

    [Fact]
    public void CreateInitializesAnInboxItem()
    {
        TodoItem todo = TodoItem.Create("  Design task model  ", null, null);

        Assert.Equal("Design task model", todo.Title);
        Assert.Equal(TodoStatus.Inbox, todo.Status);
        Assert.Equal(TodoQuadrant.NotImportantNotUrgent, todo.GetQuadrant(Now, UrgentThreshold));
        Assert.Empty(todo.Subtasks);
        Assert.Equal(0, todo.ProgressPercent);
    }

    [Fact]
    public void DueWithinThresholdIsUrgent()
    {
        TodoItem todo = TodoItem.Create("Review migrations", null, Now.AddHours(12));

        TodoQuadrant quadrant = todo.GetQuadrant(Now, UrgentThreshold);

        Assert.Equal(TodoQuadrant.NotImportantUrgent, quadrant);
    }

    [Fact]
    public void MovingToQuadrantOverridesImportanceAndUrgency()
    {
        TodoItem todo = TodoItem.Create("Write tests", null, Now.AddDays(7));

        todo.MoveToQuadrant(TodoQuadrant.ImportantUrgent);

        Assert.True(todo.IsImportant);
        Assert.True(todo.IsUrgentOverride);
        Assert.Equal(TodoQuadrant.ImportantUrgent, todo.GetQuadrant(Now, UrgentThreshold));
    }

    [Fact]
    public void CompletingSubtasksUpdatesProgressWithoutCompletingParent()
    {
        TodoItem todo = TodoItem.Create("Build calendar", null, null);
        TodoSubtask first = todo.AddSubtask("Domain model");
        todo.AddSubtask("Desktop view");

        first.Complete(Now);

        Assert.Equal(50, todo.ProgressPercent);
        Assert.Equal(TodoStatus.Inbox, todo.Status);
    }

    [Fact]
    public void CompleteRecordsTimestampAndReopenClearsIt()
    {
        TodoItem todo = TodoItem.Create("Ship build", null, null);

        todo.Complete(Now);
        Assert.Equal(TodoStatus.Completed, todo.Status);
        Assert.Equal(Now, todo.CompletedAtUtc);
        Assert.Equal(100, todo.ProgressPercent);

        todo.Reopen();
        Assert.Equal(TodoStatus.NotStarted, todo.Status);
        Assert.Null(todo.CompletedAtUtc);
    }

    [Fact]
    public void MoveToStatusSupportsKanbanStatesButRequiresCompleteForCompletion()
    {
        TodoItem todo = TodoItem.Create("Implement board", null, null);

        todo.MoveToStatus(TodoStatus.InProgress);

        Assert.Equal(TodoStatus.InProgress, todo.Status);
        Assert.Throws<InvalidOperationException>(() => todo.MoveToStatus(TodoStatus.Completed));
    }

    [Fact]
    public void SetPlanningWindowStoresValidRange()
    {
        TodoItem todo = TodoItem.Create("Plan gantt row", null, null);

        todo.SetPlanningWindow(Now, Now.AddDays(3));

        Assert.Equal(Now, todo.StartAtUtc);
        Assert.Equal(Now.AddDays(3), todo.DueAtUtc);
        Assert.Throws<ArgumentException>(() => todo.SetPlanningWindow(Now, Now.AddMinutes(-1)));
    }

    [Fact]
    public void AddDependencyRejectsSelfAndDuplicate()
    {
        TodoItem todo = TodoItem.Create("Dependent task", null, null);
        Guid dependencyId = Guid.NewGuid();

        todo.AddDependency(dependencyId);

        Assert.Single(todo.Dependencies);
        Assert.Throws<ArgumentException>(() => todo.AddDependency(todo.Id));
        Assert.Throws<InvalidOperationException>(() => todo.AddDependency(dependencyId));
    }
}
