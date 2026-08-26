using CcCalendar.Core.Projects;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class ProjectWorkspaceViewModelTests
{
    [Fact]
    public void LoadBuildsProgressKanbanMilestonesAndGanttRows()
    {
        Project project = Project.Create(
            "cccalendar",
            "#246BCE",
            new DateOnly(2026, 8, 16),
            new DateOnly(2026, 12, 31));
        project.AddMilestone("First usable build", new DateOnly(2026, 9, 1));
        TodoItem active = TodoItem.Create("Build project page", project.Id, null);
        active.MoveToStatus(TodoStatus.InProgress);
        active.SetPlanningWindow(
            new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));
        TodoItem completed = TodoItem.Create("Define requirements", project.Id, null);
        completed.Complete(new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero));
        var viewModel = new ProjectWorkspaceViewModel();

        viewModel.Load([project], [active, completed]);

        Assert.Equal(project.Id, viewModel.SelectedProject!.Id);
        Assert.Equal(50, viewModel.ProgressPercent);
        Assert.Single(viewModel.KanbanColumns.Single(column => column.Status == TodoStatus.InProgress).Items);
        Assert.Single(viewModel.Milestones);
        Assert.Single(viewModel.GanttRows);
        Assert.Equal(18, viewModel.GanttRows[0].BarOffset);
        Assert.Equal(72, viewModel.GanttRows[0].BarWidth);
    }
}
