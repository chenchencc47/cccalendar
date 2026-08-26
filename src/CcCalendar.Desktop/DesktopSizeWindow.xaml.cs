using System.Globalization;
using System.Windows;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop;

public partial class DesktopSizeWindow : Window
{
    public DesktopSizeWindow(double width, double height)
    {
        InitializeComponent();
        WidthBox.Text = width.ToString("F0", CultureInfo.CurrentCulture);
        HeightBox.Text = height.ToString("F0", CultureInfo.CurrentCulture);
    }

    public DesktopWindowSize SelectedSize { get; private set; }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectedSize = DesktopSizeEditorState.Parse(WidthBox.Text, HeightBox.Text);
            DialogResult = true;
        }
        catch (ArgumentException)
        {
            ValidationText.Text = "请输入有效的数字。";
        }
    }
}
