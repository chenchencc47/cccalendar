using CcCalendar.Core.Projects;
using CcCalendar.Core.Todos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class ProjectWorkspaceViewModel : ObservableObject
{
    private Project? selectedProject;
    private int progressPercent;
    private IReadOnlyList<TodoItem> allTodos = [];

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public Project? SelectedProject
    {
        get => selectedProject;
        set
        {
            if (SetProperty(ref selectedProject, value))
            {
                RefreshSelectedProject();
            }
        }
    }

    public int ProgressPercent
    {
        get => progressPercent;
        private set => SetProperty(ref progressPercent, value);
    }

    public int TotalCount { get; private set; }

    public int CompletedCount { get; private set; }

    public int ActiveCount { get; private set; }

    public IReadOnlyList<KanbanColumnViewModel> KanbanColumns { get; private set; } = [];

    public IReadOnlyList<Milestone> Milestones { get; private set; } = [];

    public IReadOnlyList<GanttRowViewModel> GanttRows { get; private set; } = [];

    public bool HasProjects => Projects.Count > 0;

    public void Load(IEnumerable<Project> projects, IEnumerable<TodoItem> todos)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(todos);

        Projects = [.. projects];
        allTodos = [.. todos];
        SelectedProject = Projects.Count > 0 ? Projects[0] : null;
        OnPropertyChanged(nameof(Projects));
        OnPropertyChanged(nameof(HasProjects));
    }

    private void RefreshSelectedProject()
    {
        TodoItem[] projectTodos = SelectedProject is null
            ? []
            : [.. allTodos.Where(todo => todo.ProjectId == SelectedProject.Id)];

        TotalCount = projectTodos.Length;
        CompletedCount = projectTodos.Count(todo => todo.Status == TodoStatus.Completed);
        ActiveCount = projectTodos.Length - CompletedCount;
        ProgressPercent = projectTodos.Length == 0
            ? 0
            : CompletedCount * 100 / projectTodos.Length;
        KanbanColumns = CreateKanbanColumns(projectTodos);
        Milestones = SelectedProject?.Milestones ?? [];
        DateOnly timelineStart = SelectedProject?.StartDate
            ?? projectTodos
                .Where(todo => todo.StartAtUtc.HasValue)
                .Select(todo => DateOnly.FromDateTime(todo.StartAtUtc!.Value.UtcDateTime))
                .DefaultIfEmpty()
                .Min();
        GanttRows = [.. projectTodos
            .Where(todo => todo.StartAtUtc.HasValue && todo.DueAtUtc.HasValue)
            .Select(todo => CreateGanttRow(todo, timelineStart))];

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(KanbanColumns));
        OnPropertyChanged(nameof(Milestones));
        OnPropertyChanged(nameof(GanttRows));
    }

    private static IReadOnlyList<KanbanColumnViewModel> CreateKanbanColumns(IReadOnlyList<TodoItem> todos)
    {
        return
        [
            CreateColumn(TodoStatus.Inbox, "收集箱", todos),
            CreateColumn(TodoStatus.NotStarted, "待开始", todos),
            CreateColumn(TodoStatus.InProgress, "进行中", todos),
            CreateColumn(TodoStatus.Blocked, "已阻塞", todos),
            CreateColumn(TodoStatus.Completed, "已完成", todos),
        ];
    }

    private static KanbanColumnViewModel CreateColumn(
        TodoStatus status,
        string title,
        IReadOnlyList<TodoItem> todos)
    {
        return new KanbanColumnViewModel(
            status,
            title,
            [.. todos.Where(todo => todo.Status == status)]);
    }

    private static GanttRowViewModel CreateGanttRow(TodoItem todo, DateOnly timelineStart)
    {
        DateOnly startDate = DateOnly.FromDateTime(todo.StartAtUtc!.Value.UtcDateTime);
        DateOnly dueDate = DateOnly.FromDateTime(todo.DueAtUtc!.Value.UtcDateTime);
        const double pixelsPerDay = 18;
        return new GanttRowViewModel(
            todo.Id,
            todo.Title,
            startDate,
            dueDate,
            (startDate.DayNumber - timelineStart.DayNumber) * pixelsPerDay,
            (dueDate.DayNumber - startDate.DayNumber + 1) * pixelsPerDay);
    }
}
