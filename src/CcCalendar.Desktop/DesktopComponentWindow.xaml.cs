using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CcCalendar.Core.Calendars;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Infrastructure.Persistence;

namespace CcCalendar.Desktop;

public partial class DesktopComponentWindow : Window, IDesktopWindowBehaviorTarget, IDesktopAppearanceTarget
{
    private readonly DesktopAppearanceController appearanceController;
    private readonly DesktopWindowBehaviorController behaviorController;
    private readonly CalendarDataService dataService;
    private readonly Func<Guid, Task> scheduleDeleteRequested;
    private readonly Func<Guid, Task> todoCompleteRequested;
    private readonly Func<Window, Task> todoCreationRequested;
    private readonly Func<DateOnly, Window, Task> scheduleRequested;
    private readonly Func<Guid, Window, Task> scheduleEditRequested;
    private readonly DesktopCalendarLayoutState? layoutState;
    private readonly DesktopPanelLayoutState panelLayoutState;
    private readonly DesktopComponentViewModel viewModel;
    private bool hasBeenPositioned;
    private bool isApplyingLayout;

    public DesktopComponentWindow(
        DesktopComponentKind kind,
        DesktopCalendarLayoutSettings? calendarLayoutSettings = null,
        DesktopPanelLayoutSettings? panelLayoutSettings = null,
        DesktopWindowBehaviorSettings? behaviorSettings = null,
        DesktopAppearanceSettings? appearanceSettings = null,
        IChineseCalendarService? chineseCalendarService = null,
        Func<DateOnly, Window, Task>? scheduleRequested = null,
        Func<Guid, Window, Task>? scheduleEditRequested = null,
        DesktopComponentVisibilityActions? visibilityActions = null,
        Func<Guid, Task>? scheduleDeleteRequested = null,
        Func<Guid, Task>? todoCompleteRequested = null,
        Func<Window, Task>? todoCreationRequested = null)
    {
        InitializeComponent();
        appearanceController = new DesktopAppearanceController(
            this,
            ChromeBorder,
            appearanceSettings ?? new DesktopAppearanceSettings());
        behaviorController = new DesktopWindowBehaviorController(
            this,
            behaviorSettings ?? new DesktopWindowBehaviorSettings());
        this.scheduleRequested = scheduleRequested ?? ((_, _) => Task.CompletedTask);
        this.scheduleEditRequested = scheduleEditRequested ?? ((_, _) => Task.CompletedTask);
        this.scheduleDeleteRequested = scheduleDeleteRequested ?? (_ => Task.CompletedTask);
        this.todoCompleteRequested = todoCompleteRequested ?? (_ => Task.CompletedTask);
        this.todoCreationRequested = todoCreationRequested ?? (_ => Task.CompletedTask);
        DesktopComponentDefinition definition = DesktopComponentDefinition.ForKind(kind);
        panelLayoutState = new DesktopPanelLayoutState(panelLayoutSettings ?? new DesktopPanelLayoutSettings
        {
            Width = definition.DefaultSize.Width,
            Height = definition.DefaultSize.Height,
        });
        Title = definition.Title;
        Width = panelLayoutState.Current.Width;
        Height = panelLayoutState.Current.Height;
        if (kind == DesktopComponentKind.Calendar)
        {
            layoutState = new DesktopCalendarLayoutState(
                calendarLayoutSettings ?? new DesktopCalendarLayoutSettings());
            DesktopCalendarSize initialSize = layoutState.CurrentSize;
            Width = initialSize.Width;
            Height = initialSize.Height;
        }

        else if (panelLayoutState.Current.Left.HasValue && panelLayoutState.Current.Top.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = panelLayoutState.Current.Left.Value;
            Top = panelLayoutState.Current.Top.Value;
            hasBeenPositioned = true;
        }

        viewModel = new DesktopComponentViewModel(
            kind,
            TimeProvider.System,
            chineseCalendarService);
        if (layoutState is not null)
        {
            viewModel.Workbench.Calendar.Mode = ToViewMode(layoutState.Mode);
            viewModel.Workbench.Calendar.PropertyChanged += CalendarPropertyChanged;
        }

        // 右键快速新增：日程组件新增日程、待办组件新增待办。
        Action? quickCreate = kind switch
        {
            DesktopComponentKind.Agenda => () => _ = this.scheduleRequested(
                viewModel.Workbench.SelectedDate,
                this),
            DesktopComponentKind.Todo => () => _ = this.todoCreationRequested(this),
            _ => null,
        };
        string? quickCreateHeader = kind switch
        {
            DesktopComponentKind.Agenda => "新增日程...",
            DesktopComponentKind.Todo => "新增待办...",
            _ => null,
        };
        if (visibilityActions is not null)
        {
            DesktopComponentContextMenu.Attach(
                this,
                kind,
                visibilityActions,
                EditSize,
                quickCreateRequested: quickCreate,
                quickCreateHeader: quickCreateHeader,
                isMousePassthroughEnabled: () => BehaviorSettings.IsMousePassthrough,
                toggleMousePassthrough: ToggleMousePassthrough);
        }

        SizeChanged += WindowSizeChanged;
        LocationChanged += WindowLocationChanged;
        DataContext = viewModel;
        dataService = new CalendarDataService(ApplicationPaths.DatabasePath);
        Loaded += WindowLoaded;
    }

    public DesktopCalendarLayoutSettings? LayoutSettings => layoutState?.ToSettings();

    public DesktopPanelLayoutSettings PanelLayoutSettings => panelLayoutState.ToSettings();

    public DesktopWindowBehaviorSettings BehaviorSettings => behaviorController.Settings;

    public DesktopAppearanceSettings AppearanceSettings => appearanceController.Settings;

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            PositionIfUnset();
            Show();
            behaviorController.ReapplyNativeBehavior();
        }
    }

    public void TogglePositionLock() => behaviorController.TogglePositionLock();

    public void ToggleMousePassthrough() => behaviorController.ToggleMousePassthrough();

    public void DisableMousePassthrough() => behaviorController.DisableMousePassthrough();

    public void SetLayer(DesktopWindowLayer layer) => behaviorController.SetLayer(layer);

    public void ToggleEdgeAutoHide() => behaviorController.ToggleEdgeAutoHide();

    public void ApplyAppearance(DesktopAppearanceSettings settings) => appearanceController.Apply(settings);

    public async Task ReloadAsync()
    {
        await dataService.InitializeAsync(CancellationToken.None);
        CalendarDataSnapshot snapshot = await dataService.LoadAsync(CancellationToken.None);
        viewModel.Workbench.Load(snapshot.CalendarEvents, snapshot.Todos);
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void CalendarDayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CalendarDayViewModel day })
        {
            // 拦截冒泡，避免双击的第二次按下被窗口拖动等逻辑吞掉。
            e.Handled = true;
            if (e.ClickCount == 2)
            {
                await scheduleRequested(day.Date, this);
            }
        }
    }

    private async void EventDetailsClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DateOnly date })
        {
            viewModel.Workbench.SelectDate(date);
            var dialog = new DayScheduleWindow(
                date,
                viewModel.Workbench.Calendar.GetEventDetails(date))
            {
                Owner = this,
            };
            bool? addRequested = dialog.ShowDialog();
            foreach (Guid eventId in dialog.DeletedEventIds)
            {
                await scheduleDeleteRequested(eventId);
            }

            // 详情窗里点了「编辑」：弹出编辑窗预填原内容；否则结果为 true 表示新增。
            if (dialog.EditedEventId is { } eventIdToEdit)
            {
                await scheduleEditRequested(eventIdToEdit, this);
            }
            else if (addRequested == true)
            {
                await scheduleRequested(date, this);
            }

            e.Handled = true;
        }
    }

    private async void DeleteAgendaEventClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid eventId }
            || MessageBox.Show(
                this,
                "确定删除这项日程吗？删除后可从回收站恢复。",
                "删除日程",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await scheduleDeleteRequested(eventId);
        e.Handled = true;
    }

    private async void CompleteTodoClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid todoId })
        {
            await todoCompleteRequested(todoId);
            e.Handled = true;
        }
    }

    private void WindowHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && behaviorController.CanMove)
        {
            DragMove();
            behaviorController.HandleMoveCompleted();
            e.Handled = true;
        }
    }

    private void PositionIfUnset()
    {
        if (hasBeenPositioned)
        {
            return;
        }

        Rect workArea = SystemParameters.WorkArea;
        (Left, Top) = viewModel.Kind switch
        {
            DesktopComponentKind.Calendar => (workArea.Left + 24, workArea.Top + 24),
            DesktopComponentKind.Agenda => (workArea.Right - Width - 24, workArea.Top + 24),
            DesktopComponentKind.Todo => (workArea.Right - Width - 24, workArea.Bottom - Height - 24),
            _ => (workArea.Left, workArea.Top),
        };
        hasBeenPositioned = true;
    }

    private void CalendarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (layoutState is null || e.PropertyName != nameof(CalendarViewModel.Mode))
        {
            return;
        }

        DesktopCalendarSize size = layoutState.SwitchTo(
            viewModel.Workbench.Calendar.Mode == CalendarViewMode.Week
                ? DesktopCalendarMode.Week
                : DesktopCalendarMode.Month);
        isApplyingLayout = true;
        Width = size.Width;
        Height = size.Height;
        isApplyingLayout = false;
    }

    private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!isApplyingLayout && IsLoaded && WindowState == WindowState.Normal)
        {
            if (layoutState is not null)
            {
                layoutState.RememberSize(e.NewSize.Width, e.NewSize.Height);
            }
            else
            {
                panelLayoutState.RememberBounds(e.NewSize.Width, e.NewSize.Height, Left, Top);
            }
        }
    }

    private void WindowLocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        if (layoutState is not null)
        {
            layoutState.RememberPosition(Left, Top);
        }
        else
        {
            panelLayoutState.RememberBounds(Width, Height, Left, Top);
        }
    }

    private void EditSize()
    {
        var dialog = new DesktopSizeWindow(Width, Height) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Width = dialog.SelectedSize.Width;
            Height = dialog.SelectedSize.Height;
        }
    }

    private static CalendarViewMode ToViewMode(DesktopCalendarMode mode)
    {
        return mode == DesktopCalendarMode.Week
            ? CalendarViewMode.Week
            : CalendarViewMode.Month;
    }
}
