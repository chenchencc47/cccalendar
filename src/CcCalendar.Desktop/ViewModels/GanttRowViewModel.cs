namespace CcCalendar.Desktop.ViewModels;

public sealed record GanttRowViewModel(
    Guid TodoItemId,
    string Title,
    DateOnly StartDate,
    DateOnly DueDate,
    double BarOffset,
    double BarWidth);
