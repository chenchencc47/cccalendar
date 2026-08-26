using CcCalendar.Core.Todos;

namespace CcCalendar.Desktop.ViewModels;

public sealed record KanbanColumnViewModel(
    TodoStatus Status,
    string Title,
    IReadOnlyList<TodoItem> Items);
