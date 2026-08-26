using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CcCalendar.Core.Tools;

namespace CcCalendar.Desktop;

public partial class RegionSelectorWindow : Window
{
    private Point dragStart;

    public RegionSelectorWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public CaptureRegion? SelectedRegion { get; private set; }

    private void WindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(SelectionCanvas);
        CaptureMouse();
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(dragStart);
    }

    private void WindowMouseMove(object sender, MouseEventArgs e)
    {
        if (IsMouseCaptured)
        {
            UpdateSelection(e.GetPosition(SelectionCanvas));
        }
    }

    private void WindowMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured)
        {
            return;
        }

        Point end = e.GetPosition(SelectionCanvas);
        ReleaseMouseCapture();
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        try
        {
            SelectedRegion = CaptureRegion.FromDrag(
                (int)Math.Round((Left + dragStart.X) * dpi.DpiScaleX),
                (int)Math.Round((Top + dragStart.Y) * dpi.DpiScaleY),
                (int)Math.Round((Left + end.X) * dpi.DpiScaleX),
                (int)Math.Round((Top + end.Y) * dpi.DpiScaleY));
            DialogResult = true;
        }
        catch (ArgumentException)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void UpdateSelection(Point end)
    {
        double left = Math.Min(dragStart.X, end.X);
        double top = Math.Min(dragStart.Y, end.Y);
        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);
        SelectionBorder.Width = Math.Abs(end.X - dragStart.X);
        SelectionBorder.Height = Math.Abs(end.Y - dragStart.Y);
    }
}
