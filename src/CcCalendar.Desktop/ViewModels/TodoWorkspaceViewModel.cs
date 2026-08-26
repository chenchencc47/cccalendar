using CcCalendar.Core.Todos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class TodoWorkspaceViewModel : ObservableObject
{
    private static readonly TimeSpan UrgentThreshold = TimeSpan.FromHours(24);

    private readonly TimeProvider timeProvider;
    private TodoViewMode mode;
    private IReadOnlyList<TodoItem> allItems = [];

    public TodoWorkspaceViewModel(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public TodoViewMode Mode
    {
        get => mode;
        set => SetProperty(ref mode, value);
    }

    public IReadOnlyList<TodoItem> ListItems { get; private set; } = [];

    public IReadOnlyList<TodoQuadrantGroupViewModel> Quadrants { get; private set; } = [];

    public IReadOnlyList<KanbanColumnViewModel> BoardColumns { get; private set; } = [];

    public void Load(IEnumerable<TodoItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        allItems = [.. items];
        RefreshViews();
    }

    public void MoveToQuadrant(TodoItem item, TodoQuadrant quadrant)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.MoveToQuadrant(quadrant);
        RefreshViews();
    }

    public void MoveToStatus(TodoItem item, TodoStatus status)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (status == TodoStatus.Completed)
        {
            item.Complete(timeProvider.GetUtcNow());
        }
        else
        {
            item.MoveToStatus(status);
        }

        RefreshViews();
    }

    private void RefreshViews()
    {
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();
        ListItems = [.. allItems.OrderBy(item => item.Status).ThenBy(item => item.DueAtUtc)];
        Quadrants =
        [
            CreateQuadrant(TodoQuadrant.ImportantUrgent, "重要且紧急", nowUtc),
            CreateQuadrant(TodoQuadrant.ImportantNotUrgent, "重要不紧急", nowUtc),
            CreateQuadrant(TodoQuadrant.NotImportantUrgent, "不重要但紧急", nowUtc),
            CreateQuadrant(TodoQuadrant.NotImportantNotUrgent, "不重要不紧急", nowUtc),
        ];
        BoardColumns =
        [
            CreateColumn(TodoStatus.Inbox, "收集箱"),
            CreateColumn(TodoStatus.NotStarted, "待开始"),
            CreateColumn(TodoStatus.InProgress, "进行中"),
            CreateColumn(TodoStatus.Blocked, "已阻塞"),
            CreateColumn(TodoStatus.Completed, "已完成"),
        ];

        OnPropertyChanged(nameof(ListItems));
        OnPropertyChanged(nameof(Quadrants));
        OnPropertyChanged(nameof(BoardColumns));
    }

    private TodoQuadrantGroupViewModel CreateQuadrant(
        TodoQuadrant quadrant,
        string title,
        DateTimeOffset nowUtc)
    {
        return new TodoQuadrantGroupViewModel(
            quadrant,
            title,
            [.. allItems.Where(item => item.GetQuadrant(nowUtc, UrgentThreshold) == quadrant)]);
    }

    private KanbanColumnViewModel CreateColumn(TodoStatus status, string title)
    {
        return new KanbanColumnViewModel(
            status,
            title,
            [.. allItems.Where(item => item.Status == status)]);
    }
}
