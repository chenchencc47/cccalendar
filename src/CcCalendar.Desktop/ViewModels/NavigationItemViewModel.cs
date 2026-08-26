using MahApps.Metro.IconPacks;

namespace CcCalendar.Desktop.ViewModels;

public sealed record NavigationItemViewModel(
    NavigationDestination Destination,
    string Label,
    PackIconLucideKind Icon);
