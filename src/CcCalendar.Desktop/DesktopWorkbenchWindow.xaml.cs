using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CcCalendar.Core.Calendars;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Infrastructure.Persistence;

namespace CcCalendar.Desktop;

public partial class DesktopWorkbenchWindow : Window, IDesktopWindowBehaviorTarget, IDesktopAppearanceTarget
{
    private readonly DesktopAppearanceController appearanceController;
    private readonly DesktopWindowBehaviorController behaviorController;
    private readonly CalendarDataService dataService;
    private readonly Func<Guid, Task> scheduleDeleteRequested;
    private readonly Func<DateOnly, Window, Task> scheduleRequested;
    private readonly Func<Guid, Window, Task> scheduleEditRequested;
    private readonly DesktopCalendarLayoutState layoutState;
    private readonly DesktopWorkbenchViewModel viewModel;
    private bool isApplyingLayout;
    private bool isHorizontalDragging;
    private Point horizontalDragStartScreen;
    private double horizontalDragStartLeft;
    private double horizontalDragTop;

    public DesktopWorkbenchWindow(
        DesktopCalendarLayoutSettings layoutSettings,
        DesktopWindowBehaviorSettings behaviorSettings,
        DesktopAppearanceSettings appearanceSettings,
        IChineseCalendarService? chineseCalendarService = null,
        Func<DateOnly, Window, Task>? scheduleRequested = null,
        Func<Guid, Window, Task>? scheduleEditRequested = null,
        DesktopComponentVisibilityActions? visibilityActions = null,
        Func<Guid, Task>? scheduleDeleteRequested = null)
    {
        InitializeComponent();
        appearanceController = new DesktopAppearanceController(this, ChromeBorder, appearanceSettings);
        behaviorController = new DesktopWindowBehaviorController(this, behaviorSettings);
        layoutState = new DesktopCalendarLayoutState(layoutSettings);
        this.scheduleRequested = scheduleRequested ?? ((_, _) => Task.CompletedTask);
        this.scheduleEditRequested = scheduleEditRequested ?? ((_, _) => Task.CompletedTask);
        this.scheduleDeleteRequested = scheduleDeleteRequested ?? (_ => Task.CompletedTask);
        DesktopCalendarSize initialSize = layoutState.CurrentSize;
        Width = initialSize.Width;
        Height = initialSize.Height;
        if (!layoutState.IsTopCenterPinned
            && layoutSettings.Left.HasValue
            && layoutSettings.Top.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = layoutSettings.Left.Value;
            Top = layoutSettings.Top.Value;
        }
        else if (layoutState.IsTopCenterPinned && layoutSettings.Left.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = layoutSettings.Left.Value;
        }

        if (visibilityActions is not null)
        {
            DesktopComponentContextMenu.Attach(
                this,
                DesktopComponentKind.Calendar,
                visibilityActions,
                EditSize,
                () => layoutState.IsTopCenterPinned,
                ToggleTopCenterPinned,
                () => BehaviorSettings.IsEdgeAutoHideEnabled,
                ToggleEdgeAutoHide,
                isMousePassthroughEnabled: () => BehaviorSettings.IsMousePassthrough,
                toggleMousePassthrough: ToggleMousePassthrough);
        }

        dataService = new CalendarDataService(ApplicationPaths.DatabasePath);
        viewModel = new DesktopWorkbenchViewModel(TimeProvider.System, chineseCalendarService);
        viewModel.Calendar.Mode = ToViewMode(layoutState.Mode);
        DataContext = viewModel;
        Loaded += WindowLoaded;
        SizeChanged += WindowSizeChanged;
        LocationChanged += WindowLocationChanged;
        viewModel.Calendar.PropertyChanged += CalendarPropertyChanged;
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed
                && e.OriginalSource is not ButtonBase
                && e.OriginalSource is not ResizeGrip
                && behaviorController.CanMove)
            {
                BeginHorizontalDrag(e);
            }
        };
        MouseMove += HorizontalDragMouseMove;
        MouseLeftButtonUp += HorizontalDragMouseLeftButtonUp;
    }

    public DesktopCalendarLayoutSettings LayoutSettings => layoutState.ToSettings();

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
            Show();
            Activate();
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
        viewModel.Load(snapshot.CalendarEvents, snapshot.Todos);
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        if (layoutState.IsTopCenterPinned)
        {
            PositionTopCenter();
        }

        behaviorController.InitializeEdgeAutoHide();
        await ReloadAsync();
    }

    private async void DayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CalendarDayViewModel day })
        {
            viewModel.SelectDate(day.Date);
            // 立即拦截冒泡：否则单击会冒泡到窗口级 MouseLeftButtonDown 触发 DragMove，
            // 其模态移动循环会吞掉双击的第二次按下，导致双击非当天日期无法新增。
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
            viewModel.SelectDate(date);
            var dialog = new DayScheduleWindow(
                date,
                viewModel.Calendar.GetEventDetails(date))
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

    private void CalendarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CalendarViewModel.Mode))
        {
            return;
        }

        DesktopCalendarSize size = layoutState.SwitchTo(ToDesktopMode(viewModel.Calendar.Mode));
        isApplyingLayout = true;
        Width = size.Width;
        Height = size.Height;
        isApplyingLayout = false;
    }

    private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!isApplyingLayout && WindowState == WindowState.Normal)
        {
            layoutState.RememberSize(e.NewSize.Width, e.NewSize.Height);
            if (IsLoaded && layoutState.IsTopCenterPinned)
            {
                PositionTopCenter();
            }
        }
    }

    private void WindowLocationChanged(object? sender, EventArgs e)
    {
        if (IsLoaded && WindowState == WindowState.Normal)
        {
            layoutState.RememberHorizontalPosition(Left);
        }
    }

    private void ToggleTopCenterPinned()
    {
        layoutState.ToggleTopCenterPinned();
        if (layoutState.IsTopCenterPinned)
        {
            PositionTopCenter();
            behaviorController.HandleMoveCompleted();
        }
        else
        {
            layoutState.RememberPosition(Left, Top);
        }
    }

    private void PositionTopCenter()
    {
        Rect workArea = SystemParameters.WorkArea;
        DesktopWindowPosition position = DesktopTopCenterLayout.Calculate(
            new DesktopWindowBounds(workArea.Left, workArea.Top, workArea.Width, workArea.Height),
            Width);
        if (!layoutState.ToSettings().Left.HasValue)
        {
            Left = position.Left;
        }
        Top = position.Top;
    }

    private void BeginHorizontalDrag(MouseButtonEventArgs e)
    {
        horizontalDragStartScreen = PointToScreen(e.GetPosition(this));
        horizontalDragStartLeft = Left;
        horizontalDragTop = Top;
        isHorizontalDragging = true;
        CaptureMouse();
        e.Handled = true;
    }

    private void HorizontalDragMouseMove(object sender, MouseEventArgs e)
    {
        if (!isHorizontalDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point currentScreen = PointToScreen(e.GetPosition(this));
        Left = horizontalDragStartLeft + currentScreen.X - horizontalDragStartScreen.X;
        Top = horizontalDragTop;
    }

    private void HorizontalDragMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isHorizontalDragging)
        {
            return;
        }

        isHorizontalDragging = false;
        ReleaseMouseCapture();
        layoutState.RememberHorizontalPosition(Left);
        behaviorController.HandleMoveCompleted();
        e.Handled = true;
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

    private static DesktopCalendarMode ToDesktopMode(CalendarViewMode mode)
    {
        return mode == CalendarViewMode.Week
            ? DesktopCalendarMode.Week
            : DesktopCalendarMode.Month;
    }
}
