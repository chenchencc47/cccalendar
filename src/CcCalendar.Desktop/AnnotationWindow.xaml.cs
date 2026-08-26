using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CcCalendar.Core.Tools;
using CcCalendar.Desktop.Tools;
using Microsoft.Win32;

namespace CcCalendar.Desktop;

public partial class AnnotationWindow : Window
{
    private bool mosaicMode;
    private Point mosaicStart;
    private ScreenshotZoomState zoom;

    public AnnotationWindow(BitmapSource screenshot)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        InitializeComponent();
        zoom = new ScreenshotZoomState(100);
        ScreenshotImage.Source = screenshot;
        AnnotationSurface.Width = screenshot.PixelWidth;
        AnnotationSurface.Height = screenshot.PixelHeight;
        Ink.Width = screenshot.PixelWidth;
        Ink.Height = screenshot.PixelHeight;
        Ink.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Colors.Red,
            Width = 3,
            Height = 3,
            FitToCurve = true,
        };
    }

    private void PenClick(object sender, RoutedEventArgs e) => Ink.EditingMode = InkCanvasEditingMode.Ink;

    private void EraseClick(object sender, RoutedEventArgs e) => Ink.EditingMode = InkCanvasEditingMode.EraseByStroke;

    private void ClearClick(object sender, RoutedEventArgs e) => Ink.Strokes.Clear();

    private void MosaicClick(object sender, RoutedEventArgs e)
    {
        mosaicMode = true;
        Ink.EditingMode = InkCanvasEditingMode.None;
        Ink.Cursor = Cursors.Cross;
    }

    private async void OcrClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = await WindowsOcrService.RecognizeAsync(
                RenderAnnotation(),
                CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(text))
            {
                Clipboard.SetText(text);
            }

            MessageBox.Show(
                this,
                string.IsNullOrWhiteSpace(text) ? "未识别到文字。" : "识别文字已复制到剪贴板。",
                "OCR",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "OCR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PinClick(object sender, RoutedEventArgs e)
    {
        var pinned = new PinnedImageWindow(RenderAnnotation());
        pinned.Show();
    }

    private void CopyClick(object sender, RoutedEventArgs e) => Clipboard.SetImage(RenderAnnotation());

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".png",
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = $"cccalendar-capture-{DateTime.Now:yyyyMMdd-HHmmss}.png",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        BitmapSource bitmap = RenderAnnotation();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(dialog.FileName);
        encoder.Save(stream);
    }

    private void ZoomOutClick(object sender, RoutedEventArgs e) =>
        ZoomSlider.Value = zoom.Step(-1).Percent;

    private void ZoomInClick(object sender, RoutedEventArgs e) =>
        ZoomSlider.Value = zoom.Step(1).Percent;

    private void ZoomSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        zoom = zoom.SetPercent((int)e.NewValue);
        if (AnnotationZoomTransform is null || ZoomText is null)
        {
            return;
        }

        AnnotationZoomTransform.ScaleX = zoom.Scale;
        AnnotationZoomTransform.ScaleY = zoom.Scale;
        ZoomText.Text = $"{zoom.Percent}%";
    }

    private RenderTargetBitmap RenderAnnotation()
    {
        AnnotationSurface.Measure(new Size(AnnotationSurface.Width, AnnotationSurface.Height));
        AnnotationSurface.Arrange(new Rect(0, 0, AnnotationSurface.Width, AnnotationSurface.Height));
        var bitmap = new RenderTargetBitmap(
            (int)AnnotationSurface.Width,
            (int)AnnotationSurface.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(AnnotationSurface);
        bitmap.Freeze();
        return bitmap;
    }

    private void InkPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (mosaicMode)
        {
            mosaicStart = e.GetPosition(Ink);
            e.Handled = true;
        }
    }

    private void InkPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!mosaicMode || ScreenshotImage.Source is not BitmapSource source)
        {
            return;
        }

        Point end = e.GetPosition(Ink);
        try
        {
            CaptureRegion region = CaptureRegion.FromDrag(
                (int)mosaicStart.X,
                (int)mosaicStart.Y,
                (int)end.X,
                (int)end.Y);
            ScreenshotImage.Source = MosaicImageProcessor.Apply(source, region);
        }
        catch (ArgumentException)
        {
        }
        finally
        {
            mosaicMode = false;
            Ink.EditingMode = InkCanvasEditingMode.Ink;
            Ink.Cursor = Cursors.Pen;
            e.Handled = true;
        }
    }
}
