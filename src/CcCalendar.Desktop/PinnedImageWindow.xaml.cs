using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CcCalendar.Desktop.Tools;

namespace CcCalendar.Desktop;

public partial class PinnedImageWindow : Window
{
    private ScreenshotZoomState zoom;

    public PinnedImageWindow(BitmapSource image)
    {
        InitializeComponent();
        zoom = new ScreenshotZoomState(100);
        ArgumentNullException.ThrowIfNull(image);
        PinnedImage.Source = image;
        double fit = Math.Min(500 / image.Width, 304 / image.Height);
        fit = Math.Min(1, fit);
        PinnedImage.Width = Math.Max(1, image.Width * fit);
        PinnedImage.Height = Math.Max(1, image.Height * fit);
    }

    private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ZoomOutClick(object sender, RoutedEventArgs e) => ApplyZoom(zoom.Step(-1));

    private void ZoomInClick(object sender, RoutedEventArgs e) => ApplyZoom(zoom.Step(1));

    private void ApplyZoom(ScreenshotZoomState value)
    {
        zoom = value;
        PinnedZoomTransform.ScaleX = zoom.Scale;
        PinnedZoomTransform.ScaleY = zoom.Scale;
        ZoomText.Text = $"{zoom.Percent}%";
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
