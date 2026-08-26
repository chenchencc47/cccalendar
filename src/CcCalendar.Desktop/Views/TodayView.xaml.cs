using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public partial class TodayView : UserControl
{
    public TodayView()
    {
        InitializeComponent();
    }

    private void SearchQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter
            || DataContext is not TodayViewModel { Weather: { } weather }
            || !weather.SearchCommand.CanExecute(null))
        {
            return;
        }

        weather.SearchCommand.Execute(null);
        e.Handled = true;
    }

    private void QuickAddScheduleClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            _ = mainWindow.OpenQuickAddAsync();
        }
    }

    private void QuickAddTodoClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            _ = mainWindow.OpenQuickAddAsync(initialKind: QuickAddKind.Todo);
        }
    }
}
