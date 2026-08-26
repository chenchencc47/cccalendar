using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class GlobalSearchViewModel : ObservableObject
{
    private IReadOnlyList<SearchCandidate> candidates = [];
    private IReadOnlyList<GlobalSearchResultViewModel> results = [];
    private string query = string.Empty;
    private GlobalSearchItemKind? kindFilter;

    public string Query
    {
        get => query;
        set
        {
            if (SetProperty(ref query, value ?? string.Empty))
            {
                Refresh();
            }
        }
    }

    public GlobalSearchItemKind? KindFilter
    {
        get => kindFilter;
        set
        {
            if (SetProperty(ref kindFilter, value))
            {
                Refresh();
            }
        }
    }

    public IReadOnlyList<GlobalSearchResultViewModel> Results
    {
        get => results;
        private set
        {
            if (SetProperty(ref results, value))
            {
                OnPropertyChanged(nameof(HasResults));
            }
        }
    }

    public bool HasResults => Results.Count > 0;

    public void Load(
        IEnumerable<Project> projects,
        IEnumerable<TodoItem> todos,
        IEnumerable<CalendarEvent> calendarEvents,
        IEnumerable<WorkRecord> records)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(calendarEvents);
        ArgumentNullException.ThrowIfNull(records);

        candidates =
        [
            .. projects.Select(project => CreateCandidate(
                project.Id,
                GlobalSearchItemKind.Project,
                project.Name,
                "项目",
                NavigationDestination.Projects,
                project.Name)),
            .. todos.Select(todo => CreateCandidate(
                todo.Id,
                GlobalSearchItemKind.Todo,
                todo.Title,
                "待办",
                NavigationDestination.Todos,
                todo.Title)),
            .. calendarEvents.Select(calendarEvent => CreateCandidate(
                calendarEvent.Id,
                GlobalSearchItemKind.Event,
                calendarEvent.Title,
                "日程",
                NavigationDestination.Calendar,
                calendarEvent.Title)),
            .. records.Select(record => CreateCandidate(
                record.Id,
                GlobalSearchItemKind.Record,
                record.Title,
                "记录",
                NavigationDestination.Records,
                $"{record.Title}\n{record.CurrentContent}")),
        ];
        Refresh();
    }

    private static SearchCandidate CreateCandidate(
        Guid entityId,
        GlobalSearchItemKind kind,
        string title,
        string subtitle,
        NavigationDestination destination,
        string searchableText)
    {
        return new SearchCandidate(
            new GlobalSearchResultViewModel(entityId, kind, title, subtitle, destination),
            searchableText);
    }

    private void Refresh()
    {
        string normalizedQuery = Query.Trim();

        if (normalizedQuery.Length == 0)
        {
            Results = [];
            return;
        }

        Results = [.. candidates
            .Where(candidate => !KindFilter.HasValue || candidate.Result.Kind == KindFilter)
            .Where(candidate => candidate.SearchableText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.Result)];
    }

    private sealed record SearchCandidate(
        GlobalSearchResultViewModel Result,
        string SearchableText);
}
