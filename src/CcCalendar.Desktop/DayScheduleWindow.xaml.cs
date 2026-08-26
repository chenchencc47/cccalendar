using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop;

public partial class DayScheduleWindow : Window
{
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    public DayScheduleWindow(
        DateOnly date,
        IReadOnlyList<CalendarEventDetailViewModel> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        InitializeComponent();
        Date = date;
        Events = new ObservableCollection<CalendarEventDetailViewModel>(events);
        DateText = date.ToString("yyyy年M月d日 dddd", ChineseCulture);
        CountText = $"{events.Count} 项日程";
        DataContext = this;
    }

    public DateOnly Date { get; }

    public ObservableCollection<CalendarEventDetailViewModel> Events { get; }

    public List<Guid> DeletedEventIds { get; } = [];

    public Guid? EditedEventId { get; private set; }

    public string DateText { get; }

    public string CountText { get; }

    private void EditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid eventId })
        {
            StartEdit(eventId);
        }
    }

    private void EventMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { Tag: Guid eventId })
        {
            StartEdit(eventId);
            e.Handled = true;
        }
    }

    private void StartEdit(Guid eventId)
    {
        EditedEventId = eventId;
        DialogResult = true;
    }

    private void DeleteClick(object sender, RoutedEventArgs e)
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

        CalendarEventDetailViewModel calendarEvent = Events.Single(item => item.Id == eventId);
        DeletedEventIds.Add(eventId);
        Events.Remove(calendarEvent);
        CountTextBlock.Text = $"{Events.Count} 项日程";
    }

    private void AddClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
