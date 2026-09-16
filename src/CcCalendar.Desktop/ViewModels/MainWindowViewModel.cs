using CcCalendar.Core.Calendars;
using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;

namespace CcCalendar.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private NavigationItemViewModel selectedItem;

    public MainWindowViewModel(
        TimeProvider timeProvider,
        AppearanceSettingsViewModel? settings = null,
        IChineseCalendarService? chineseCalendarService = null,
        WeatherViewModel? weather = null,
        AssistantViewModel? assistant = null,
        ToolCenterViewModel? tools = null)
    {
        NavigationItems =
        [
            new(NavigationDestination.Today, "今天", PackIconLucideKind.CalendarDays),
            new(NavigationDestination.Calendar, "日历", PackIconLucideKind.CalendarRange),
            new(NavigationDestination.Projects, "项目", PackIconLucideKind.FolderKanban),
            new(NavigationDestination.Todos, "待办", PackIconLucideKind.ListChecks),
            new(NavigationDestination.Records, "记录", PackIconLucideKind.NotebookPen),
            new(NavigationDestination.Assistant, "助理", PackIconLucideKind.Bot),
            new(NavigationDestination.Statistics, "统计", PackIconLucideKind.ChartNoAxesColumnIncreasing),
            new(NavigationDestination.Tools, "工具", PackIconLucideKind.Wrench),
            new(NavigationDestination.Settings, "设置", PackIconLucideKind.Settings),
        ];
        selectedItem = NavigationItems[0];
        Today = new TodayViewModel(timeProvider, chineseCalendarService, weather);
        Calendar = new CalendarViewModel(timeProvider, chineseCalendarService);
        Projects = new ProjectWorkspaceViewModel();
        Todos = new TodoWorkspaceViewModel(timeProvider);
        Records = new RecordWorkspaceViewModel(timeProvider);
        Assistant = assistant;
        Tools = tools;
        Search = new GlobalSearchViewModel();
        Statistics = new StatisticsViewModel(timeProvider);
        Settings = settings ?? new AppearanceSettingsViewModel(
            new(),
            new(),
            new(),
            new());
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public TodayViewModel Today { get; }

    public CalendarViewModel Calendar { get; }

    public ProjectWorkspaceViewModel Projects { get; }

    public TodoWorkspaceViewModel Todos { get; }

    public RecordWorkspaceViewModel Records { get; }

    public AssistantViewModel? Assistant { get; }

    public ToolCenterViewModel? Tools { get; }

    public GlobalSearchViewModel Search { get; }

    public StatisticsViewModel Statistics { get; }

    public AppearanceSettingsViewModel Settings { get; }

    public NavigationItemViewModel SelectedItem
    {
        get => selectedItem;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (SetProperty(ref selectedItem, value))
            {
                OnPropertyChanged(nameof(SelectedPageTitle));
                OnPropertyChanged(nameof(IsTodaySelected));
                OnPropertyChanged(nameof(IsCalendarSelected));
                OnPropertyChanged(nameof(IsProjectsSelected));
                OnPropertyChanged(nameof(IsTodosSelected));
                OnPropertyChanged(nameof(IsRecordsSelected));
                OnPropertyChanged(nameof(IsAssistantSelected));
                OnPropertyChanged(nameof(IsToolsSelected));
                OnPropertyChanged(nameof(IsStatisticsSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                OnPropertyChanged(nameof(IsKnownPageSelected));
            }
        }
    }

    public string SelectedPageTitle => SelectedItem.Label;

    public bool IsTodaySelected => SelectedItem.Destination == NavigationDestination.Today;

    public bool IsCalendarSelected => SelectedItem.Destination == NavigationDestination.Calendar;

    public bool IsProjectsSelected => SelectedItem.Destination == NavigationDestination.Projects;

    public bool IsTodosSelected => SelectedItem.Destination == NavigationDestination.Todos;

    public bool IsRecordsSelected => SelectedItem.Destination == NavigationDestination.Records;

    public bool IsAssistantSelected => SelectedItem.Destination == NavigationDestination.Assistant;

    public bool IsToolsSelected => SelectedItem.Destination == NavigationDestination.Tools;

    public bool IsStatisticsSelected => SelectedItem.Destination == NavigationDestination.Statistics;

    public bool IsSettingsSelected => SelectedItem.Destination == NavigationDestination.Settings;

    public bool IsKnownPageSelected => IsTodaySelected
        || IsCalendarSelected
        || IsProjectsSelected
        || IsTodosSelected
        || IsRecordsSelected
        || IsAssistantSelected
        || IsToolsSelected
        || IsStatisticsSelected
        || IsSettingsSelected;
}
