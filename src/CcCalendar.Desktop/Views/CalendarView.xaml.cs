using System.Windows;
using System.Windows.Controls;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public partial class CalendarView : UserControl
{
    public event Action<DateOnly>? ScheduleRequested;

    public event Action<DateOnly>? DetailsRequested;

    public CalendarView()
    {
        InitializeComponent();
    }

    private void DayMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CalendarDayViewModel day })
        {
            return;
        }

        if (DataContext is CalendarViewModel viewModel)
        {
            viewModel.SelectDate(day.Date);
        }

        if (e.ClickCount == 2)
        {
            ScheduleRequested?.Invoke(day.Date);
            e.Handled = true;
        }
    }

    private void AddSelectedDateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CalendarViewModel viewModel)
        {
            ScheduleRequested?.Invoke(viewModel.SelectedDate);
        }
    }

    private void EventDetailsClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DateOnly date })
        {
            DetailsRequested?.Invoke(date);
            e.Handled = true;
        }
    }

    private void RoomBookingClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CalendarViewModel viewModel)
        {
            DetailsRequested?.Invoke(viewModel.RoomViewDate);
            e.Handled = true;
        }
    }
}
