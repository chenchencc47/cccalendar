using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class TodoWorkspaceViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LoadBuildsQuadrantsBoardAndListFromSameItems()
    {
        TodoItem urgent = TodoItem.Create("Urgent", null, Now.AddHours(1));
        urgent.MoveToQuadrant(TodoQuadrant.ImportantUrgent);
        TodoItem inbox = TodoItem.Create("Inbox", null, null);
        var viewModel = new TodoWorkspaceViewModel(new FixedTimeProvider(Now));

        viewModel.Load([urgent, inbox]);

        Assert.Equal(2, viewModel.ListItems.Count);
        Assert.Single(viewModel.Quadrants.Single(group => group.Quadrant == TodoQuadrant.ImportantUrgent).Items);
        Assert.Equal(2, viewModel.BoardColumns.Sum(column => column.Items.Count));
    }

    [Fact]
    public void MoveCommandsUpdateDomainAndRebuildViews()
    {
        TodoItem todo = TodoItem.Create("Move me", null, null);
        var viewModel = new TodoWorkspaceViewModel(new FixedTimeProvider(Now));
        viewModel.Load([todo]);

        viewModel.MoveToQuadrant(todo, TodoQuadrant.ImportantNotUrgent);
        viewModel.MoveToStatus(todo, TodoStatus.Completed);

        Assert.Equal(TodoQuadrant.ImportantNotUrgent, todo.GetQuadrant(Now, TimeSpan.FromHours(24)));
        Assert.Equal(TodoStatus.Completed, todo.Status);
        Assert.Equal(Now, todo.CompletedAtUtc);
        Assert.Single(viewModel.BoardColumns.Single(column => column.Status == TodoStatus.Completed).Items);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
