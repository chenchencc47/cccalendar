using CcCalendar.Core.Todos;

namespace CcCalendar.Desktop.ViewModels;

public sealed record TodoQuadrantGroupViewModel(
    TodoQuadrant Quadrant,
    string Title,
    IReadOnlyList<TodoItem> Items);
