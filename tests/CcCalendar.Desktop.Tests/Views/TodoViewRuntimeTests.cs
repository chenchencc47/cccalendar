using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// 真实实例化 TodoView（STA + 主题资源 + 布局），验证四象限模式下
/// 看板视图的外层 ScrollViewer 折叠且不遮挡卡片命中测试。
/// 此前折叠样式只挂在内部 ItemsControl 上，外层 ScrollViewer 始终可见，
/// 覆盖整个四象限区域并吞掉鼠标事件，导致四象限无法拖动。
/// </summary>
public sealed class TodoViewRuntimeTests
{
    [Fact]
    public void BoardScrollerIsCollapsedAndDoesNotCoverQuadrantCardsInQuadrantsMode()
    {
        RunOnSta(() =>
        {
            var timeProvider = new FrozenTimeProvider(new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8)));
            var workspace = new TodoWorkspaceViewModel(timeProvider)
            {
                Mode = TodoViewMode.Quadrants,
            };
            TodoItem todo = TodoItem.Create("拖动验证待办", null, null);
            workspace.Load([todo]);

            var view = new TodoView { DataContext = workspace, Width = 960, Height = 640 };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            try
            {
                window.Show();
                DoEvents(window);

                // 看板外层 ScrollViewer 的直接父级是内容 Grid（区别于 TextBox/DataGrid 模板内部的滚动器）。
                var allScrollers = FindVisualChildren<ScrollViewer>(view).ToList();
                string diag = string.Join("; ", allScrollers.Select(s =>
                    $"{s.Visibility}@parent{VisualTreeHelper.GetParent(s)?.GetType().Name ?? "null"}"));
                ScrollViewer? boardScroller = allScrollers
                    .FirstOrDefault(scroller => VisualTreeHelper.GetParent(scroller) is Grid);

                Assert.True(
                    boardScroller is not null && boardScroller.Visibility == Visibility.Collapsed,
                    $"四象限模式下看板外层 ScrollViewer 必须存在且整体折叠。诊断: {diag}");

                // 命中测试卡片中心：结果必须属于四象限分支（卡片 Border 或其子孙），
                // 而不是看板 ScrollViewer 分支。
                Border? card = FindCardBorder(view, todo);
                Assert.True(
                    card is not null && card.ActualWidth > 0 && card.ActualHeight > 0,
                    $"卡片应完成布局。实际: {(card is null ? "null" : $"{card.ActualWidth}x{card.ActualHeight}")}");
                Point center = new(card!.ActualWidth / 2, card.ActualHeight / 2);
                HitTestResult? hit = VisualTreeHelper.HitTest(view, card.TranslatePoint(center, view));
                Assert.NotNull(hit);
                DependencyObject? node = hit.VisualHit;
                bool insideCard = false;
                while (node is not null)
                {
                    if (ReferenceEquals(node, card))
                    {
                        insideCard = true;
                        break;
                    }

                    node = VisualTreeHelper.GetParent(node);
                }

                Assert.True(insideCard, $"四象限模式下卡片中心命中测试命中了 {hit.VisualHit.GetType().Name}，不在卡片内，拖拽会失效。");
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Border? FindCardBorder(TodoView view, TodoItem todo)
    {
        return FindVisualChildren<Border>(view)
            .FirstOrDefault(border => border.DataContext == todo);
    }

    private static bool IsInsideDataGrid(DependencyObject element)
    {
        DependencyObject? node = VisualTreeHelper.GetParent(element);
        while (node is not null)
        {
            if (node is DataGrid)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void DoEvents(Window window)
    {
        for (int i = 0; i < 3; i++)
        {
            window.Dispatcher.Invoke(DispatcherPriority.Loaded, () => { });
            window.Dispatcher.Invoke(DispatcherPriority.Render, () => { });
            window.Dispatcher.Invoke(DispatcherPriority.Background, () => { });
            window.UpdateLayout();
        }
    }

    private static void RunOnSta(Action action) => WpfRuntimeHost.Run(action);

    private sealed class FrozenTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone(
            "Frozen", localNow.Offset, null, null);
    }
}
