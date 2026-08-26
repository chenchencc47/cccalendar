using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CcCalendar.Desktop.Tools;
using CcCalendar.Desktop.ViewModels;
using Microsoft.Win32;

namespace CcCalendar.Desktop.Views;

public partial class ToolsView : UserControl
{
    private readonly DispatcherTimer timer;

    public ToolsView()
    {
        InitializeComponent();
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += TimerTick;
        IsVisibleChanged += VisibilityChanged;
    }

    private async void TimerTick(object? sender, EventArgs e)
    {
        if (DataContext is ToolCenterViewModel viewModel)
        {
            await viewModel.TickAsync(CancellationToken.None);
        }
    }

    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    private async void CaptureFullScreenClick(object sender, RoutedEventArgs e)
    {
        Window? owner = Window.GetWindow(this);
        try
        {
            owner?.Hide();
            await Task.Delay(150);
            OpenAnnotation(ScreenshotCaptureService.CapturePrimaryScreen(), owner);
        }
        finally
        {
            owner?.Show();
            owner?.Activate();
        }
    }

    private void CaptureWindowClick(object sender, RoutedEventArgs e)
    {
        Window? owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        nint handle = new WindowInteropHelper(owner).Handle;
        OpenAnnotation(ScreenshotCaptureService.CaptureWindow(handle), owner);
    }

    private void CaptureRegionClick(object sender, RoutedEventArgs e)
    {
        Window? owner = Window.GetWindow(this);
        try
        {
            owner?.Hide();
            var selector = new RegionSelectorWindow();
            if (selector.ShowDialog() == true && selector.SelectedRegion.HasValue)
            {
                OpenAnnotation(ScreenshotCaptureService.CaptureRegion(selector.SelectedRegion.Value), owner);
            }
        }
        finally
        {
            owner?.Show();
            owner?.Activate();
        }
    }

    private async void CaptureLongClick(object sender, RoutedEventArgs e)
    {
        Window? owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        nint handle = new WindowInteropHelper(owner).Handle;
        BitmapSource screenshot = await LongScreenshotService.CaptureWindowAsync(
            handle,
            3,
            80,
            CancellationToken.None);
        OpenAnnotation(screenshot, owner);
    }

    private void PinImageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
            Title = "选择贴屏图片",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(dialog.FileName, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        new PinnedImageWindow(image).Show();
    }

    private async void SearchClipboardClick(object sender, RoutedEventArgs e)
    {
        await SearchClipboardAsync();
    }

    private async void ClipboardSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SearchClipboardAsync();
        }
    }

    private async void ToggleClipboardFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ToolCenterViewModel viewModel)
        {
            await viewModel.ToggleClipboardFavoriteAsync(CancellationToken.None);
        }
    }

    private async void RestoreClipboardClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ToolCenterViewModel viewModel)
        {
            return;
        }

        CcCalendar.Core.Tools.ClipboardPayload? payload =
            await viewModel.GetSelectedClipboardPayloadAsync(CancellationToken.None);
        if (payload?.Text is not null)
        {
            Clipboard.SetText(payload.Text);
        }
        else if (payload?.ImagePath is not null)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(payload.ImagePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            Clipboard.SetImage(image);
        }
        else if (payload?.FilePaths.Count > 0)
        {
            var paths = new StringCollection();
            paths.AddRange([.. payload.FilePaths]);
            Clipboard.SetFileDropList(paths);
        }
    }

    private async Task SearchClipboardAsync()
    {
        if (DataContext is ToolCenterViewModel viewModel)
        {
            await viewModel.SearchClipboardAsync(CancellationToken.None);
        }
    }

    private static void OpenAnnotation(
        System.Windows.Media.Imaging.BitmapSource screenshot,
        Window? owner)
    {
        var annotation = new AnnotationWindow(screenshot);
        if (owner?.IsVisible == true)
        {
            annotation.Owner = owner;
        }

        annotation.ShowDialog();
    }
}
