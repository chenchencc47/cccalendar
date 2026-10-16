using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CcCalendar.Desktop.ViewModels;
using Microsoft.Win32;

namespace CcCalendar.Desktop.Views;

public partial class AssistantView : UserControl
{
    // P70-06a：会话栏两态宽度（展开 240 / rail 48）。
    private const double SessionsPaneExpandedWidth = 240;
    private const double SessionsPaneRailWidth = 48;

    // P73：开合动画时长/淡入淡出节奏（毫秒）。
    private const int SessionsPaneWidthDuration = 240;
    private const int SessionsPanePanelFadeOut = 110;
    private const int SessionsPanePanelFadeIn = 180;
    private const int SessionsPaneButtonFade = 100;

    /// <summary>
    /// P73：会话栏开合动画开关。生产默认开；运行时测试置 false——
    /// 动画时钟依赖渲染节拍，同步断言无法等动画走完（见
    /// SessionsPaneCollapseTogglesRailWidthBothWays）。
    /// </summary>
    internal static bool PaneAnimationEnabled { get; set; } = true;

    private AssistantViewModel? subscribedViewModel;
    private NotifyCollectionChangedEventHandler? messagesChangedHandler;
    private AssistantViewModel? paneWidthViewModel;
    // P74-03：上下文分段条的渲染订阅（段块宽度依赖 VM 比例，无法纯 XAML 绑定）。
    private AssistantViewModel? contextMeterViewModel;

    public AssistantView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is AssistantViewModel viewModel)
            {
                AttachAutoScroll(viewModel);
            }

            ScrollToLatestMessage();
        };
        Unloaded += (_, _) => DetachAutoScroll();
        if (DataContext is AssistantViewModel viewModel)
        {
            AttachAutoScroll(viewModel);
        }

        DataContextChanged += (_, _) =>
        {
            DetachAutoScroll();
            if (DataContext is AssistantViewModel viewModel)
            {
                AttachAutoScroll(viewModel);
            }
        };
    }

    private void AttachAutoScroll(AssistantViewModel viewModel)
    {
        DetachAutoScroll();
        subscribedViewModel = viewModel;
        messagesChangedHandler = (_, _) => ScrollToLatestMessageIfNearBottom();
        viewModel.Messages.CollectionChanged += messagesChangedHandler;
        // P70-06a：会话栏列宽跟随收起状态。ColumnDefinition.Width 无法用
        // DataTrigger 的 Setter 命令（不在模板内），由 VM 属性变更回调驱动。
        // P73：attach 时按 VM 当前态直切（无动画）；后续切换走平滑开合。
        DetachSessionsPaneWidth();
        paneWidthViewModel = viewModel;
        viewModel.PropertyChanged += SessionsPanePropertyChanged;
        SetSessionsPaneState(viewModel.IsSessionsPaneCollapsed, animate: false);
        // P74-03：分段条跟随上下文构成事件重渲染（attach 时按当前态先画一次）。
        DetachContextMeter();
        contextMeterViewModel = viewModel;
        viewModel.PropertyChanged += ContextMeterPropertyChanged;
        RenderContextMeter(viewModel);
    }

    private void DetachAutoScroll()
    {
        if (subscribedViewModel is not null && messagesChangedHandler is not null)
        {
            subscribedViewModel.Messages.CollectionChanged -= messagesChangedHandler;
        }

        subscribedViewModel = null;
        messagesChangedHandler = null;
        DetachSessionsPaneWidth();
        DetachContextMeter();
    }

    private void DetachSessionsPaneWidth()
    {
        if (paneWidthViewModel is not null)
        {
            paneWidthViewModel.PropertyChanged -= SessionsPanePropertyChanged;
        }

        paneWidthViewModel = null;
    }

    private void DetachContextMeter()
    {
        if (contextMeterViewModel is not null)
        {
            contextMeterViewModel.PropertyChanged -= ContextMeterPropertyChanged;
        }

        contextMeterViewModel = null;
    }

    private void ContextMeterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssistantViewModel.ContextSegments)
            && sender is AssistantViewModel viewModel)
        {
            RenderContextMeter(viewModel);
        }
    }

    /// <summary>
    /// P74-03：按 ContextSegments 比例渲染分段条——条 100% = 已用上下文，
    /// 各段宽 = 段 tokens / 已用 × 条宽（ColumnDefinition.Width 不可绑定，
    /// 故由 code-behind 重建段块；dsh-context Current Context 的条形态）。
    /// </summary>
    private void RenderContextMeter(AssistantViewModel viewModel)
    {
        ContextMeterBar.Children.Clear();
        long used = viewModel.ContextUsedTokens;
        if (used <= 0)
        {
            return;
        }

        foreach (AssistantViewModel.ContextSegmentInfo segment in viewModel.ContextSegments)
        {
            if (segment.Tokens <= 0)
            {
                continue;
            }

            ContextMeterBar.Children.Add(new Border
            {
                Width = ContextMeterBarShell.Width * segment.Tokens / used,
                Background = (Brush)new BrushConverter().ConvertFromString(segment.Color)!,
            });
        }
    }

    private void SessionsPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssistantViewModel.IsSessionsPaneCollapsed)
            && sender is AssistantViewModel viewModel)
        {
            SetSessionsPaneState(viewModel.IsSessionsPaneCollapsed, animate: true);
        }
    }

    /// <summary>
    /// P73：会话栏开合统一落点。直切（attach / 测试）＝列宽 + 两端可见性一步到位；
    /// 动画（生产切换）＝列宽 240↔48 补间（GridLengthAnimation），收起时面板
    /// 快速淡出让位（列宽收缩时内容不重排抖动）、rail 展开按钮淡入，展开时反向。
    /// 可见性终态在宽度动画 Completed 落定，且按 VM 当前态校准——动画期间用户
    /// 反向点击时旧动画被 BeginAnimation 替换、Completed 不会再触发（WPF 语义）。
    /// </summary>
    private void SetSessionsPaneState(bool collapsed, bool animate)
    {
        if (!animate || !PaneAnimationEnabled)
        {
            SessionsPaneColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            SessionsPaneColumn.Width = new GridLength(
                collapsed ? SessionsPaneRailWidth : SessionsPaneExpandedWidth);
            SessionsPanel.BeginAnimation(UIElement.OpacityProperty, null);
            SessionsExpandButton.BeginAnimation(UIElement.OpacityProperty, null);
            SessionsPanel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            SessionsExpandButton.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            SessionsPanel.Opacity = 1;
            SessionsExpandButton.Opacity = 1;
            return;
        }

        double sourceWidth = collapsed ? SessionsPaneExpandedWidth : SessionsPaneRailWidth;
        double targetWidth = collapsed ? SessionsPaneRailWidth : SessionsPaneExpandedWidth;

        // 过渡期间两端都可见，靠透明度交叉淡化表达方向。
        SessionsPanel.Visibility = Visibility.Visible;
        SessionsExpandButton.Visibility = Visibility.Visible;

        var fadePanel = new DoubleAnimation(
            collapsed ? 1 : 0,
            collapsed ? 0 : 1,
            TimeSpan.FromMilliseconds(collapsed ? SessionsPanePanelFadeOut : SessionsPanePanelFadeIn))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            // 展开：先让列宽动起来再淡入面板，避免 48px 窄列里先挤出一瞬内容。
            BeginTime = collapsed ? TimeSpan.Zero : TimeSpan.FromMilliseconds(60),
        };
        SessionsPanel.BeginAnimation(UIElement.OpacityProperty, fadePanel);

        var fadeButton = new DoubleAnimation(
            collapsed ? 0 : 1,
            collapsed ? 1 : 0,
            TimeSpan.FromMilliseconds(SessionsPaneButtonFade));
        SessionsExpandButton.BeginAnimation(UIElement.OpacityProperty, fadeButton);

        var width = new GridLengthAnimation
        {
            From = new GridLength(sourceWidth),
            To = new GridLength(targetWidth),
            Duration = TimeSpan.FromMilliseconds(SessionsPaneWidthDuration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        width.Completed += (_, _) =>
        {
            bool nowCollapsed = paneWidthViewModel?.IsSessionsPaneCollapsed ?? collapsed;
            SessionsPaneColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            SessionsPaneColumn.Width = new GridLength(
                nowCollapsed ? SessionsPaneRailWidth : SessionsPaneExpandedWidth);
            SessionsPanel.Visibility = nowCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SessionsExpandButton.Visibility = nowCollapsed ? Visibility.Visible : Visibility.Collapsed;
            SessionsPanel.Opacity = nowCollapsed ? 0 : 1;
            SessionsExpandButton.Opacity = nowCollapsed ? 1 : 0;
        };
        SessionsPaneColumn.BeginAnimation(ColumnDefinition.WidthProperty, width);
    }

    private void ScrollToLatestMessage()
    {
        if (MessageList.Items.Count > 0)
        {
            MessageList.ScrollIntoView(MessageList.Items[^1]);
        }
    }

    private void ScrollToLatestMessageIfNearBottom()
    {
        ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(MessageList);
        if (scrollViewer is not null
            && scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset > 48)
        {
            return;
        }

        ScrollToLatestMessage();
    }

    private void MessageListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(MessageList);
        if (scrollViewer is null)
        {
            return;
        }

        double nextOffset = scrollViewer.VerticalOffset - e.Delta * 0.45;
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(nextOffset, 0, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void InputBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Shift+Enter：由 TextBox 自行插入换行（AcceptsReturn=True）。
            return;
        }

        // Enter：直接发送消息。
        if (DataContext is AssistantViewModel viewModel
            && viewModel.SendCommand.CanExecute(null))
        {
            viewModel.SendCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void CopyMessageClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string text }
            && !string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }

    // P70-04：两枚会话级统计胶囊的 Popup 在页面级，x:Name 直接可达。
    private void SessionStatsPillClick(object sender, RoutedEventArgs e) =>
        SessionStatsPopup.IsOpen = true;

    private void TokenStatsPillClick(object sender, RoutedEventArgs e) =>
        TokenStatsPopup.IsOpen = true;

    // P74-03：上下文构成胶囊——点击弹图例，分段条按 VM 比例渲染。
    private void ContextMeterPillClick(object sender, RoutedEventArgs e) =>
        ContextMeterPopup.IsOpen = true;

    // P72 composer 改版：三个芯片菜单的 Popup 都挂在页面级（StaysOpen=False，
    // 点击外部自动关），点击只负责切换开合；菜单条目点击后收起即可——
    // 权限档由 EnumEqualsConverter 双向绑定写回 Mode，模型由 ListBox 选中写回。
    private void ComposerAddMenuClick(object sender, RoutedEventArgs e) =>
        ComposerAddPopup.IsOpen = !ComposerAddPopup.IsOpen;

    private void PermissionChipClick(object sender, RoutedEventArgs e) =>
        PermissionPopup.IsOpen = !PermissionPopup.IsOpen;

    private void PermissionMenuItemClick(object sender, RoutedEventArgs e) =>
        PermissionPopup.IsOpen = false;

    private void ModelChipClick(object sender, RoutedEventArgs e) =>
        ModelPopup.IsOpen = !ModelPopup.IsOpen;

    private void ModelMenuSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ModelPopup.IsOpen = false;

    /// <summary>
    /// P70-04：消息级轮次用量胶囊。Popup 声明在 DataTemplate 内，模板 NameScope
    /// 里用 ElementName 绑定兄弟元素的解析时机不可靠，因此沿逻辑树找同一操作行里的
    /// Popup，把 PlacementTarget 挂到被点的胶囊上再打开。
    /// </summary>
    private void MessageUsagePillClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement pill
            || LogicalTreeHelper.GetParent(pill) is not FrameworkElement parent)
        {
            return;
        }

        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is Popup popup)
            {
                popup.PlacementTarget ??= pill;
                popup.IsOpen = true;
                return;
            }
        }
    }

    private async void AttachFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            // P74-04：图片附件走视觉桥转写（发送前先转成文字）。
            Filter = "支持的文件|*.txt;*.md;*.csv;*.json;*.ics;*.png;*.jpg;*.jpeg;*.webp;*.gif|所有文件|*.*",
            Multiselect = true,
            Title = "添加给 AI 助手",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true
            || DataContext is not AssistantViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.AddAttachmentsAsync(dialog.FileNames, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or InvalidDataException)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                exception.Message,
                "无法添加附件",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}

/// <summary>
/// P70-07：附件文件卡 meta 行的字节数格式化（"12.3 KB"）。只服务本视图，
/// 注册在 AssistantView 自己的资源里（不进 App.xaml）——作用域收敛，
/// 其他页面不感知；除法与 0.# 格式和 VM 的 AttachmentStatus 保持一致。
/// </summary>
public sealed class FileSizeTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return DependencyProperty.UnsetValue;
        }

        return $"{bytes / 1024d:0.#} KB";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}

/// <summary>
/// P70-07：发送/停止单按钮两态中「非流式=发送图标」的一侧——与
/// BooleanToVisibilityConverter 相反（IsSending=false 时可见）。同样只注册在
/// 本视图资源里；不给 PackIcon 挂 Style 是为了不干扰 MahApps 图标控件
/// 从主题字典继承的默认模板。
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
