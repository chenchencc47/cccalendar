namespace CcCalendar.Desktop.ViewModels;

public sealed record DailyCompletionPointViewModel(
    DateOnly Date,
    int Count,
    double BarHeight);
