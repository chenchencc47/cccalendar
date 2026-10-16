using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using CcCalendar.Desktop;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// P70-06b 主界面收起能力运行时测试：主导航 rail 化 + 顶部搜索栏收起。
/// MainWindow 内部按默认路径构造数据库/存储，因此用 CCCALENDAR_HOME 把
/// ApplicationPaths 重定向到临时目录，避免测试读写开发机真实数据。
/// </summary>
public sealed class MainWindowRuntimeTests
{
    /// <summary>
    /// 导航 rail（一）：以收起态启动（设置持久化恢复路径）列宽即 48、条目文字
    /// 隐藏且 rail 态有条目名 ToolTip；展开↔收起双向切换列宽与文字可见性。
    /// </summary>
    [Fact]
    public void NavRailRestoresCollapsedStateAndTogglesWidthBothWays()
    {
        RunOnSta(() =>
        {
            using MainWindowIsolation isolation = MainWindowIsolation.Create();
            var window = new MainWindow(mainNavCollapsed: true);
            try
            {
                window.Show();
                DoEvents(window);

                var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
                ColumnDefinition navColumn = Assert.IsType<ColumnDefinition>(
                    window.FindName("NavColumn"));

                // 恢复的收起态：列宽 48、文字隐藏、rail 态条目带 ToolTip。
                Assert.True(viewModel.IsNavCollapsed);
                Assert.Equal(48, navColumn.Width.Value);
                ListBox navList = FindVisualChildren<ListBox>(window)
                    .Single(list => ReferenceEquals(list.ItemsSource, viewModel.NavigationItems));
                AssertNavLabelsVisible(navList, false);
                ListBoxItem todoItem = FindVisualChildren<ListBoxItem>(navList)
                    .Single(item => AutomationProperties.GetName(item) == "待办");
                Assert.Contains(
                    FindVisualChildren<FrameworkElement>(todoItem),
                    element => element.IsVisible && Equals(element.ToolTip, "待办"));

                // 展开：列宽 200、文字恢复。
                Button expand = FindVisualChildren<Button>(window)
                    .Single(button => AutomationProperties.GetName(button) == "展开导航栏");
                ClickButton(expand);
                DoEvents(window);
                Assert.False(viewModel.IsNavCollapsed);
                Assert.Equal(200, navColumn.Width.Value);
                AssertNavLabelsVisible(navList, true);

                // 再收起：回到 rail，双向可达。
                Button collapse = FindVisualChildren<Button>(window)
                    .Single(button => AutomationProperties.GetName(button) == "收起导航栏");
                ClickButton(collapse);
                DoEvents(window);
                Assert.True(viewModel.IsNavCollapsed);
                Assert.Equal(48, navColumn.Width.Value);
                AssertNavLabelsVisible(navList, false);
            }
            finally
            {
                window.Close();
                window.Dispose();
            }
        });
    }

    /// <summary>
    /// 搜索栏收起（二）：展开态点击收起按钮后搜索框隐藏、原位出现搜索图标
    /// 按钮；点击图标按钮恢复搜索框。往返双向可达。
    /// </summary>
    [Fact]
    public void TopSearchCollapseRoundTripsViaExpandButton()
    {
        RunOnSta(() =>
        {
            using MainWindowIsolation isolation = MainWindowIsolation.Create();
            var window = new MainWindow();
            try
            {
                window.Show();
                DoEvents(window);

                var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
                Border searchBorder = Assert.IsType<Border>(window.FindName("SearchInputBorder"));
                Assert.False(viewModel.IsTopSearchCollapsed);
                Assert.True(searchBorder.IsVisible);

                // 收起：搜索框隐藏、原位只剩搜索图标按钮。
                Button collapse = FindVisualChildren<Button>(window)
                    .Single(button => AutomationProperties.GetName(button) == "收起搜索栏");
                ClickButton(collapse);
                DoEvents(window);
                Assert.True(viewModel.IsTopSearchCollapsed);
                Assert.False(searchBorder.IsVisible);
                Button expand = FindVisualChildren<Button>(window)
                    .Single(button => AutomationProperties.GetName(button) == "展开搜索栏");
                Assert.True(expand.IsVisible);

                // 展开：搜索框恢复、图标按钮让位。
                ClickButton(expand);
                DoEvents(window);
                Assert.False(viewModel.IsTopSearchCollapsed);
                Assert.True(searchBorder.IsVisible);
                Assert.False(expand.IsVisible);
            }
            finally
            {
                window.Close();
                window.Dispose();
            }
        });
    }

    /// <summary>
    /// 收起态不回归（三）：导航 rail 化后点击条目仍能切换页面——
    /// 选中「待办」后目标页可见、原页面让位。
    /// </summary>
    [Fact]
    public void NavClickStillSwitchesPagesWhileRailCollapsed()
    {
        RunOnSta(() =>
        {
            using MainWindowIsolation isolation = MainWindowIsolation.Create();
            var window = new MainWindow();
            try
            {
                window.Show();
                DoEvents(window);

                var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
                ClickButton(FindVisualChildren<Button>(window)
                    .Single(button => AutomationProperties.GetName(button) == "收起导航栏"));
                DoEvents(window);
                Assert.True(viewModel.IsNavCollapsed);

                ListBox navList = FindVisualChildren<ListBox>(window)
                    .Single(list => ReferenceEquals(list.ItemsSource, viewModel.NavigationItems));
                ListBoxItem todoItem = FindVisualChildren<ListBoxItem>(navList)
                    .Single(item => AutomationProperties.GetName(item) == "待办");
                todoItem.IsSelected = true;
                DoEvents(window);

                Assert.Equal(NavigationDestination.Todos, viewModel.SelectedItem.Destination);
                Assert.True(viewModel.IsTodosSelected);
                Assert.False(viewModel.IsTodaySelected);
                Assert.True(Assert.IsType<TodoView>(window.FindName("TodoPage")).IsVisible);
            }
            finally
            {
                window.Close();
                window.Dispose();
            }
        });
    }

    /// <summary>逐条断言导航条目文字的可见性（展开态可见 / rail 态隐藏）。</summary>
    private static void AssertNavLabelsVisible(ListBox navList, bool visible)
    {
        string[] labels = ["今天", "日历", "项目", "待办", "记录", "助理", "统计", "工具", "设置"];
        foreach (string label in labels)
        {
            ListBoxItem item = FindVisualChildren<ListBoxItem>(navList)
                .Single(container => AutomationProperties.GetName(container) == label);
            TextBlock? labelText = FindVisualChildren<TextBlock>(item)
                .SingleOrDefault(block => block.Text == label);
            if (visible)
            {
                Assert.NotNull(labelText);
                Assert.True(labelText!.IsVisible, $"导航条目「{label}」文字应在展开态可见");
            }
            else
            {
                if (labelText is not null)
                {
                    Assert.False(labelText.IsVisible, $"导航条目「{label}」文字应在收起态隐藏");
                }
            }
        }
    }

    /// <summary>
    /// MainWindow 默认按 ApplicationPaths 落盘（数据库/剪贴板目录），测试用
    /// CCCALENDAR_HOME 重定向到临时目录做隔离；WPF 运行时测试在共享 STA
    /// 宿主上串行执行，改环境变量不会与其他测试竞争。
    /// </summary>
    private sealed class MainWindowIsolation : IDisposable
    {
        private readonly string? previousHome;

        private MainWindowIsolation(string root, string? previousHome)
        {
            Root = root;
            this.previousHome = previousHome;
        }

        public string Root { get; }

        public static MainWindowIsolation Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                $"cccalendar-mainwindow-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string? previousHome = Environment.GetEnvironmentVariable("CCCALENDAR_HOME");
            Environment.SetEnvironmentVariable("CCCALENDAR_HOME", root);
            return new MainWindowIsolation(root, previousHome);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("CCCALENDAR_HOME", previousHome);
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // MainWindowLoaded 的异步续作可能仍短暂持有 SQLite 句柄，
                // 清理失败不影响断言结果，留给系统临时目录清理。
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// 按钮走 Command 绑定时，RaiseEvent(ClickEvent) 只走路由事件、不经过
    /// OnClick 的 Command 执行；借 AutomationPeer 的 IInvokeProvider.Invoke
    /// 走完整 OnClick 管线。peer 的 Invoke 把点击异步投递到 DispatcherPriority.
    /// Input，需用同优先级空回调排干（DoEvents 泵的优先级更高，轮不到 Input）。
    /// </summary>
    private static void ClickButton(Button button)
    {
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(button);
        var invokeProvider = Assert.IsAssignableFrom<System.Windows.Automation.Provider.IInvokeProvider>(peer);
        invokeProvider.Invoke();
        // 同优先级 FIFO：先投递的点击先于这个空回调执行。
        button.Dispatcher.Invoke(DispatcherPriority.Input, () => { });
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
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
            window.UpdateLayout();
        }
    }

    private static void RunOnSta(Action action) => WpfRuntimeHost.Run(action);
}
