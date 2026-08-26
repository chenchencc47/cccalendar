using System.Windows.Controls;

namespace CcCalendar.Desktop.Views;

public partial class ProjectView : UserControl
{
    public event EventHandler? CreateProjectRequested;
    public event EventHandler? DeleteProjectRequested;

    public ProjectView()
    {
        InitializeComponent();
    }

    private void CreateProjectClick(object sender, System.Windows.RoutedEventArgs e)
    {
        CreateProjectRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteProjectClick(object sender, System.Windows.RoutedEventArgs e)
    {
        DeleteProjectRequested?.Invoke(this, EventArgs.Empty);
    }
}
