using CcCalendar.Core.Calendars;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;

namespace CcCalendar.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private NavigationItemViewModel selectedItem;
    // P70-06b：两处收起状态写回设置的回调（宿主接线，VM 不直接依赖设置存储）。
    private readonly Action<bool>? mainNavCollapsedChanged;
    private readonly Action<bool>? topSearchCollapsedChanged;
    private bool isNavCollapsed;
    private bool isTopSearchCollapsed;

    public MainWindowViewModel(
        TimeProvider timeProvider,
        AppearanceSettingsViewModel? settings = null,
        IChineseCalendarService? chineseCalendarService = null,
        WeatherViewModel? weather = null,
        AssistantViewModel? assistant = null,
        ToolCenterViewModel? tools = null,
        bool mainNavCollapsed = false,
        Action<bool>? mainNavCollapsedChanged = null,
        bool topSearchCollapsed = false,
        Action<bool>? topSearchCollapsedChanged = null)
    {
        this.mainNavCollapsedChanged = mainNavCollapsedChanged;
        this.topSearchCollapsedChanged = topSearchCollapsedChanged;
        isNavCollapsed = mainNavCollapsed;
        isTopSearchCollapsed = topSearchCollapsed;
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
        // P70-06b：主导航 rail 化与顶部搜索栏收起的切换入口，XAML 按钮直接绑定。
        ToggleNavCommand = new RelayCommand(() => IsNavCollapsed = !IsNavCollapsed);
        ToggleTopSearchCommand = new RelayCommand(() => IsTopSearchCollapsed = !IsTopSearchCollapsed);
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

    /// <summary>收起/展开主界面左侧导航栏（P70-06b）。</summary>
    public IRelayCommand ToggleNavCommand { get; }

    /// <summary>收起/展开主界面顶部搜索栏（P70-06b）。</summary>
    public IRelayCommand ToggleTopSearchCommand { get; }

    /// <summary>
    /// 主界面导航栏收起状态（P70-06b）：false=展开 200px（图标+文字），true=rail 48px（仅图标）；
    /// 切换时通过回调写回设置（mainNavCollapsed）。
    /// </summary>
    public bool IsNavCollapsed
    {
        get => isNavCollapsed;
        set
        {
            if (SetProperty(ref isNavCollapsed, value))
            {
                mainNavCollapsedChanged?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// 顶部搜索栏收起状态（P70-06b）：false=搜索框可见，true=原位只留搜索图标按钮；
    /// 切换时通过回调写回设置（topSearchCollapsed）。
    /// </summary>
    public bool IsTopSearchCollapsed
    {
        get => isTopSearchCollapsed;
        set
        {
            if (SetProperty(ref isTopSearchCollapsed, value))
            {
                topSearchCollapsedChanged?.Invoke(value);
            }
        }
    }

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
