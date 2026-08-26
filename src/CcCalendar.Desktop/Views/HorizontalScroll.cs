using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CcCalendar.Desktop.Views;

/// <summary>
/// 横向滚动辅助：滚轮直接横向滚动；可选启用按住内容拖拽平移。
/// </summary>
public static class HorizontalScroll
{
    /// <summary>计算内容拖动后的偏移；内容跟随指针移动，边界处钳制。</summary>
    public static double CalculatePanOffset(double startOffset, double pointerDelta, double scrollableWidth) =>
        Math.Clamp(startOffset - pointerDelta, 0, Math.Max(0, scrollableWidth));

    public static readonly DependencyProperty EnableWheelProperty =
        DependencyProperty.RegisterAttached(
            "EnableWheel",
            typeof(bool),
            typeof(HorizontalScroll),
            new PropertyMetadata(false, OnEnableWheelChanged));

    public static readonly DependencyProperty EnableDragPanProperty =
        DependencyProperty.RegisterAttached(
            "EnableDragPan",
            typeof(bool),
            typeof(HorizontalScroll),
            new PropertyMetadata(false, OnEnableDragPanChanged));

    private static readonly DependencyProperty DragStartPointProperty =
        DependencyProperty.RegisterAttached(
            "DragStartPoint",
            typeof(Point?),
            typeof(HorizontalScroll),
            new PropertyMetadata(null));

    private static readonly DependencyProperty DragStartOffsetProperty =
        DependencyProperty.RegisterAttached(
            "DragStartOffset",
            typeof(double),
            typeof(HorizontalScroll),
            new PropertyMetadata(0.0));

    public static bool GetEnableWheel(DependencyObject obj) =>
        (bool)obj.GetValue(EnableWheelProperty);

    public static void SetEnableWheel(DependencyObject obj, bool value) =>
        obj.SetValue(EnableWheelProperty, value);

    public static bool GetEnableDragPan(DependencyObject obj) =>
        (bool)obj.GetValue(EnableDragPanProperty);

    public static void SetEnableDragPan(DependencyObject obj, bool value) =>
        obj.SetValue(EnableDragPanProperty, value);

    private static void OnEnableWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer viewer)
        {
            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            if ((bool)e.NewValue)
            {
                viewer.PreviewMouseWheel += OnPreviewMouseWheel;
            }
        }
    }

    private static void OnEnableDragPanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer viewer)
        {
            viewer.PreviewMouseLeftButtonDown -= OnPanMouseDown;
            viewer.PreviewMouseMove -= OnPanMouseMove;
            viewer.PreviewMouseLeftButtonUp -= OnPanMouseUp;
            if ((bool)e.NewValue)
            {
                viewer.PreviewMouseLeftButtonDown += OnPanMouseDown;
                viewer.PreviewMouseMove += OnPanMouseMove;
                viewer.PreviewMouseLeftButtonUp += OnPanMouseUp;
            }
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer { ScrollableWidth: > 0 } viewer)
        {
            return;
        }

        // 后滚（Delta<0）查看后面内容，前滚回看前面内容。
        viewer.ScrollToHorizontalOffset(Math.Clamp(
            viewer.HorizontalOffset - e.Delta,
            0,
            viewer.ScrollableWidth));
        e.Handled = true;
    }

    private static void OnPanMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer { ScrollableWidth: > 0 } viewer)
        {
            return;
        }

        if (IsInsideScrollBar(e.OriginalSource as DependencyObject))
        {
            return;
        }

        viewer.SetValue(DragStartPointProperty, e.GetPosition(viewer));
        viewer.SetValue(DragStartOffsetProperty, viewer.HorizontalOffset);
        // 按下即捕获：确保后续移动/抬起事件稳定送达，即使指针滑出可视区。
        viewer.CaptureMouse();
    }

    private static void OnPanMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ScrollViewer viewer
            || e.LeftButton != MouseButtonState.Pressed
            || viewer.GetValue(DragStartPointProperty) is not Point start)
        {
            return;
        }

        Point current = e.GetPosition(viewer);
        double deltaX = current.X - start.X;
        if (Math.Abs(deltaX) < SystemParameters.MinimumHorizontalDragDistance
            && viewer.HorizontalOffset == (double)viewer.GetValue(DragStartOffsetProperty))
        {
            return;
        }

        double startOffset = (double)viewer.GetValue(DragStartOffsetProperty);
        viewer.ScrollToHorizontalOffset(CalculatePanOffset(startOffset, deltaX, viewer.ScrollableWidth));
        e.Handled = true;
    }

    private static void OnPanMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollViewer viewer
            && viewer.GetValue(DragStartPointProperty) is not null)
        {
            viewer.ReleaseMouseCapture();
            viewer.SetValue(DragStartPointProperty, null);
        }
    }

    private static bool IsInsideScrollBar(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
