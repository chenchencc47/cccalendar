namespace CcCalendar.Desktop.ViewModels;

public sealed record GlobalSearchResultViewModel(
    Guid EntityId,
    GlobalSearchItemKind Kind,
    string Title,
    string Subtitle,
    NavigationDestination Destination);
