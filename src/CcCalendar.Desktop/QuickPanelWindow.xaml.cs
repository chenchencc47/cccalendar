using System.Windows;
using System.Windows.Threading;
using CcCalendar.Core.Calendars;
using CcCalendar.Desktop.Desktop;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop;

public partial class QuickPanelWindow : Window
{
    private readonly DispatcherTimer clockTimer;
    private readonly QuickPanelViewModel viewModel;

    public QuickPanelWindow(
        IChineseCalendarService? chineseCalendarService = null,
        WeatherViewModel? weather = null)
    {
        InitializeComponent();
        viewModel = new QuickPanelViewModel(TimeProvider.System, chineseCalendarService, weather);
        DataContext = viewModel;
        clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clockTimer.Tick += (_, _) => viewModel.RefreshClock();
        Deactivated += (_, _) => Hide();
        IsVisibleChanged += QuickPanelIsVisibleChanged;
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        viewModel.RefreshClock();
        Point position = QuickPanelPositioner.Calculate(
            SystemParameters.WorkArea,
            new Size(Width, Height),
            12);
        Left = position.X;
        Top = position.Y;
        Show();
        Activate();
    }

    private void QuickPanelIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            clockTimer.Start();
        }
        else
        {
            clockTimer.Stop();
        }
    }

    private void OpenMainWindowClick(object sender, RoutedEventArgs e)
    {
        Hide();
        Application.Current.MainWindow?.Show();
        Application.Current.MainWindow?.Activate();
    }
}
