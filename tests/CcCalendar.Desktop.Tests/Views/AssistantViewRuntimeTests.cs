using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.AI;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop.Tests.Views;

public sealed class AssistantViewRuntimeTests
{
    [Theory]
    [InlineData(1366, 768)]
    [InlineData(1920, 1080)]
    public void AssistantViewRendersWithoutCollapsedContentOrHorizontalOverflow(int width, int height)
    {
        RunOnSta(() =>
        {
            var viewModel = new AssistantViewModel(new RuntimeConfirmationService());
            var thinking = new AssistantMessageViewModel(false, "模型回复：会议已整理。", thinkingText: "正在读取日程并检查冲突……");
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "请整理今天的会议和待办"));
            viewModel.Messages.Add(thinking);
            // P70-02：流式思考中的消息也要能渲染——shimmer Storyboard 在模板实例化时
            // 启动，GradientStop 的 TargetName 解析失败会在这里暴露。
            var streaming = new AssistantMessageViewModel(false, string.Empty);
            streaming.AppendThinking("正在逐条核对日程");
            viewModel.Messages.Add(streaming);
            viewModel.PresentDraft(AiCreationDraft.Todo("发布前检查", null));

            var view = new AssistantView { DataContext = viewModel, Width = width, Height = height };
            var window = new Window { Content = view, Width = width, Height = height, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                Assert.True(view.ActualWidth > 0 && view.ActualHeight > 0);
                Assert.True(view.ActualWidth <= width + 1, $"助理视图横向溢出：{view.ActualWidth} > {width}");
                Assert.True(view.ActualHeight <= height + 1, $"助理视图纵向溢出：{view.ActualHeight} > {height}");
                // P70-02：思考折叠行是去按钮化的 ToggleButton（不再是 Expander），
                // 可访问名固定为「思考过程」。
                Assert.Contains(
                    FindVisualChildren<ToggleButton>(view),
                    toggle => AutomationProperties.GetName(toggle) == "思考过程");

                // shimmer 扫光确实在推进（不是静默空转）：流式标题遮罩的
                // TranslateTransform.X 一旦被动画驱动就会偏离静态值 0。
                var shimmerTitle = FindVisualChildren<TextBlock>(view)
                    .Single(block => block.Text == "思考中…");
                var translate = Assert.IsType<System.Windows.Media.TranslateTransform>(
                    Assert.IsType<System.Windows.Media.LinearGradientBrush>(shimmerTitle.OpacityMask)
                        .RelativeTransform);
                var deadline = DateTime.UtcNow.AddSeconds(2);
                bool shimmerMoves = false;
                while (DateTime.UtcNow < deadline)
                {
                    DoEvents(window);
                    if (translate.X is < -0.01 or > 0.01)
                    {
                        shimmerMoves = true;
                        break;
                    }

                    Thread.Sleep(50);
                }

                Assert.True(shimmerMoves, "shimmer 扫光动画未推进：TranslateTransform.X 停留在 0。");

                var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(view);
                Assert.True(bitmap.PixelWidth == width && bitmap.PixelHeight == height);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P70-04 统计胶囊运行时行为：无 usage 数据时整行胶囊隐藏；
    /// 收到 usage 后可见且文本正确；点击 Token 胶囊弹出详情 Popup。
    /// </summary>
    [Fact]
    public void StatsPillsStayHiddenWithoutUsageAndPopupOpensOnClick()
    {
        RunOnSta(() =>
        {
            var client = new ScriptedUsageClient(
                new AiUsageUpdate(100, 20, 40, CacheWriteTokens: 7, Model: "test-model"));
            var viewModel = new AssistantViewModel(
                new RuntimeConfirmationService(),
                assistantClient: client)
            {
                InputText = "查找发布待办",
            };
            var view = new AssistantView { DataContext = viewModel, Width = 960, Height = 680 };
            var window = new Window { Content = view, Width = 960, Height = 680, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                Button sessionPill = FindVisualChildren<Button>(view)
                    .Single(button => AutomationProperties.GetName(button) == "会话统计");
                Button tokenPill = FindVisualChildren<Button>(view)
                    .Single(button => AutomationProperties.GetName(button) == "Token 用量");

                // 无 usage 数据：整行胶囊隐藏（IsVisible 综合了祖先 Collapsed）。
                Assert.False(viewModel.HasUsageData);
                Assert.False(sessionPill.IsVisible);
                Assert.False(tokenPill.IsVisible);

                // fake 客户端全同步完成，STA 线程上直接等待不会死锁。
                viewModel.SendAsync(CancellationToken.None).GetAwaiter().GetResult();
                DoEvents(window);

                Assert.True(viewModel.HasUsageData);
                Assert.True(sessionPill.IsVisible);
                Assert.True(tokenPill.IsVisible);
                Assert.Contains(
                    FindVisualChildren<TextBlock>(view),
                    block => block.Text == "120 tokens · 40% 缓存");

                // 点击 Token 胶囊 → 详情 Popup 打开（直接触发 Click 路由事件，
                // ButtonAutomationPeer.Invoke 是显式接口实现，测试侧不可直调）。
                tokenPill.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                DoEvents(window);
                Popup tokenPopup = FindVisualChildren<Popup>(view)
                    .Single(popup => popup.PlacementTarget == tokenPill);
                Assert.True(tokenPopup.IsOpen);

                // 助手消息尾部的轮次用量胶囊也已渲染。
                Assert.Contains(
                    FindVisualChildren<Button>(view),
                    button => AutomationProperties.GetName(button) == "轮次用量" && button.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P74-03 上下文分段条运行时行为：无构成数据时隐藏；发送后可见且
    /// 分段条渲染出四段着色块；点击弹出图例 Popup（各段名称可见）。
    /// </summary>
    [Fact]
    public void ContextMeterRendersSegmentsAndOpensLegendOnClick()
    {
        RunOnSta(() =>
        {
            var model = new AiModelConfiguration(
                "deepseek",
                "DeepSeek",
                new AiProviderSettings { Model = "deepseek-v4-flash" },
                ContextWindowTokens: 131072);
            var catalog = new AiModelCatalog([model], model.Id, model.Settings);
            var viewModel = new AssistantViewModel(
                new RuntimeConfirmationService(),
                assistantClient: new CompositionClient(
                    new AiContextCompositionUpdate(1000, 2000, 3000, 500)),
                modelCatalog: catalog)
            {
                InputText = "查找发布待办",
            };
            var view = new AssistantView { DataContext = viewModel, Width = 960, Height = 680 };
            var window = new Window { Content = view, Width = 960, Height = 680, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                Button meterButton = FindVisualChildren<Button>(view)
                    .Single(button => AutomationProperties.GetName(button) == "上下文构成");

                // 发送前：没有构成数据，分段条隐藏。
                Assert.False(viewModel.HasContextComposition);
                Assert.False(meterButton.IsVisible);

                viewModel.SendAsync(CancellationToken.None).GetAwaiter().GetResult();
                DoEvents(window);

                Assert.True(viewModel.HasContextComposition);
                Assert.True(meterButton.IsVisible);

                // 分段条渲染出四段着色块（Background 已解析为实体画刷）。
                Border meterBar = FindVisualChildren<Border>(view)
                    .Single(border => AutomationProperties.GetName(border) == "上下文分段条");
                Border[] segments = [.. FindVisualChildren<Border>(meterBar)
                    .Where(border => border.Background is not null)];
                Assert.Equal(4, segments.Length);

                // 摘要文本非空（含已用缩写）。
                Assert.Contains(
                    FindVisualChildren<TextBlock>(view),
                    block => block.Text == viewModel.ContextMeterSummary
                        && viewModel.ContextMeterSummary.Contains("6.5k", StringComparison.Ordinal));

                // 点击弹出图例 Popup，四段名称可见。
                meterButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                DoEvents(window);
                Popup legend = FindVisualChildren<Popup>(view)
                    .Single(popup => popup.PlacementTarget == meterButton);
                Assert.True(legend.IsOpen);
                foreach (string name in ContextMeterLegendNames)
                {
                    Assert.Contains(
                        FindVisualChildren<TextBlock>(legend.Child),
                        block => block.Text == name);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P70-05 工具行运行时行为：折叠行渲染（可访问名=工具中文名）、状态点
    /// 存在且画刷已解析、点击展开显示详情、Ok 终态后状态点换 SuccessBrush。
    /// </summary>
    [Fact]
    public void ToolRowRendersDotExpandsAndTurnsGreenOnOk()
    {
        RunOnSta(() =>
        {
            var viewModel = new AssistantViewModel(new RuntimeConfirmationService());
            AssistantMessageViewModel toolRow = AssistantMessageViewModel.CreateToolCall(
                "query_todos", "query: release");
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "查找发布待办"));
            viewModel.Messages.Add(toolRow);

            var view = new AssistantView { DataContext = viewModel, Width = 960, Height = 680 };
            var window = new Window { Content = view, Width = 960, Height = 680, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                // 折叠行可访问名绑定 ToolDisplayName（目录中文短名）。
                ToggleButton toolToggle = FindVisualChildren<ToggleButton>(view)
                    .Single(toggle => AutomationProperties.GetName(toggle) == "查询待办");

                // 状态点存在且主题画刷已解析（Running=主题色呼吸起点）。
                System.Windows.Shapes.Ellipse dot = FindVisualChildren<System.Windows.Shapes.Ellipse>(view)
                    .Single();
                Assert.NotNull(dot.Fill);

                // 展开详情：直接切 IsChecked（RaiseEvent(Click) 不走 ToggleButton.OnClick，
                // 不会翻转 IsChecked）；可见性由 ElementName 联动到详情 Border。
                toolToggle.IsChecked = true;
                DoEvents(window);
                Assert.Contains(
                    FindVisualChildren<TextBlock>(view),
                    block => block.IsVisible && block.Text == "参数 query: release");

                // Ok 终态：状态点触发器换 SuccessBrush（颜色对主题资源取值）。
                toolRow.CompleteToolCall(AiToolCallState.Ok, "{\"items\":[]}");
                DoEvents(window);
                var successBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    System.Windows.Application.Current.Resources["SuccessBrush"]);
                var dotFill = Assert.IsType<System.Windows.Media.SolidColorBrush>(dot.Fill);
                Assert.Equal(successBrush.Color, dotFill.Color);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P70-06a 会话栏运行时行为（一）：空 VM 显示「暂无历史会话」引导；
    /// 出现历史会话后空态让位给列表；条目按标题渲染，当前项有主题色标记条；
    /// 点击条目切换 CurrentSession 并恢复该会话消息。
    /// </summary>
    [Fact]
    public void SessionsPaneShowsEmptyStateRendersEntriesAndSwitchesSessions()
    {
        RunOnSta(() =>
        {
            string filePath = Path.Combine(
                Path.GetTempPath(),
                $"cccalendar-sessions-{Guid.NewGuid():N}.json");
            try
            {
                var viewModel = new AssistantViewModel(
                    new RuntimeConfirmationService(),
                    chatHistory: new AiChatHistoryStore(filePath));
                var view = new AssistantView { DataContext = viewModel, Width = 1366, Height = 768 };
                var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    DoEvents(window);

                    // 空态：没有历史会话时显示引导文案。
                    Assert.False(viewModel.HasSessionHistory);
                    Assert.Contains(
                        FindVisualChildren<TextBlock>(view),
                        block => block.IsVisible && block.Text == "暂无历史会话");

                    // 首条消息让当前会话成为历史会话：空态让位给列表。
                    viewModel.Messages.Add(new AssistantMessageViewModel(true, "第一个会话的问题"));
                    viewModel.NewChatCommand.Execute(null);
                    DoEvents(window);
                    Assert.True(viewModel.HasSessionHistory);
                    Assert.DoesNotContain(
                        FindVisualChildren<TextBlock>(view),
                        block => block.IsVisible && block.Text == "暂无历史会话");

                    // 条目按标题渲染；当前会话条目（新对话）有主题色标记条。
                    ListBoxItem historyItem = FindVisualChildren<ListBoxItem>(view)
                        .Single(item => AutomationProperties.GetName(item) == "第一个会话的问题");
                    ListBoxItem currentItem = FindVisualChildren<ListBoxItem>(view)
                        .Single(item => AutomationProperties.GetName(item) == "新对话");
                    Border selectionBar = FindVisualChildren<Border>(currentItem)
                        .Single(border => Math.Abs(border.Width - 3) < 0.01);
                    var accentBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                        System.Windows.Application.Current.Resources["AccentBrush"]);
                    var barFill = Assert.IsType<System.Windows.Media.SolidColorBrush>(selectionBar.Background);
                    Assert.Equal(accentBrush.Color, barFill.Color);

                    // 点击历史会话条目：CurrentSession 切换、消息恢复为该会话内容。
                    historyItem.IsSelected = true;
                    DoEvents(window);
                    Assert.Same(viewModel.Sessions[1], viewModel.CurrentSession);
                    Assert.Single(viewModel.Messages);
                    Assert.Equal("第一个会话的问题", viewModel.Messages[0].Text);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(filePath);
            }
        });
    }

    /// <summary>
    /// P70-06a 会话栏运行时行为（二）：点击「收起会话栏」列宽 240→48；P72
    /// 收起干净化——Surface 面板整体收掉，rail 态只剩展开按钮可达（新对话
    /// 加号随面板隐藏）；点击「展开会话栏」恢复 240。两个状态双向可达。
    /// P73：开合改为 GridLengthAnimation 平滑过渡，动画时钟依赖渲染节拍、
    /// 同步断言等不了 240ms 补间，本用例关闭 PaneAnimationEnabled 走直切路径
    /// （动画存在性由 UiDesignContractTests 锁定）。
    /// </summary>
    [Fact]
    public void SessionsPaneCollapseTogglesRailWidthBothWays()
    {
        RunOnSta(() =>
        {
            AssistantView.PaneAnimationEnabled = false;
            var viewModel = new AssistantViewModel(new RuntimeConfirmationService());
            var view = new AssistantView { DataContext = viewModel, Width = 1366, Height = 768 };
            var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                var column = Assert.IsType<ColumnDefinition>(view.FindName("SessionsPaneColumn"));
                Assert.Equal(240, column.Width.Value);

                // 收起：列宽切到 rail 48，展开态内容隐藏、rail 按钮可达。
                Button collapse = FindVisualChildren<Button>(view)
                    .Single(button => AutomationProperties.GetName(button) == "收起会话栏");
                ClickButton(collapse);
                DoEvents(window);
                Assert.True(viewModel.IsSessionsPaneCollapsed);
                Assert.Equal(48, column.Width.Value);
                Assert.False(collapse.IsVisible);

                Button expand = FindVisualChildren<Button>(view)
                    .Single(button => AutomationProperties.GetName(button) == "展开会话栏");
                Assert.True(expand.IsVisible);
                // P72 收起干净化：面板整体收掉，新对话加号随面板隐藏，
                // rail 态不残留任何会话栏内容。
                Assert.DoesNotContain(
                    FindVisualChildren<Button>(view),
                    button => AutomationProperties.GetName(button) == "新对话" && button.IsVisible);

                // 展开：恢复 240，rail 让位、收起按钮重新可达。
                ClickButton(expand);
                DoEvents(window);
                Assert.False(viewModel.IsSessionsPaneCollapsed);
                Assert.Equal(240, column.Width.Value);
                Assert.True(collapse.IsVisible);
                Assert.False(expand.IsVisible);
            }
            finally
            {
                window.Close();
                AssistantView.PaneAnimationEnabled = true;
            }
        });
    }

    /// <summary>
    /// P70-07 单按钮两态：非流式 = Send 图标形态 + SendCommand（空输入禁用）；
    /// 流式（IsSending）= 方形停止形态 + StopCommand + Danger 底；停止收束后回发送态。
    /// </summary>
    [Fact]
    public void SendStopButtonTogglesBetweenSendAndStopStates()
    {
        RunOnSta(() =>
        {
            var client = new HeldStreamClient();
            var viewModel = new AssistantViewModel(
                new RuntimeConfirmationService(),
                assistantClient: client);
            var view = new AssistantView { DataContext = viewModel, Width = 1366, Height = 768 };
            var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
            // 测试宿主 STA 线程不在 PushFrame 内，SynchronizationContext 为空会让
            // SendAsync 的 await 续体跑到线程池，IsSending=false 的 PropertyChanged
            // 跨线程到达而收不进 DataTrigger；装上 Dispatcher 上下文模拟真实 UI 线程。
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(window.Dispatcher));
            try
            {
                window.Show();
                DoEvents(window);

                Button sendStop = FindVisualChildren<Button>(view)
                    .Single(button => AutomationProperties.GetName(button) is "发送" or "停止请求");

                // 非流式：SendCommand + 发送名；空输入时禁用（沿用原禁用逻辑）。
                Assert.Same(viewModel.SendCommand, sendStop.Command);
                Assert.Equal("发送", AutomationProperties.GetName(sendStop));
                Assert.False(sendStop.IsEnabled);
                viewModel.InputText = "流式中的问题";
                DoEvents(window);
                Assert.True(sendStop.IsEnabled);

                Task send = viewModel.SendAsync(CancellationToken.None);
                Assert.True(viewModel.IsSending);
                DoEvents(window);

                // 流式：单按钮切到停止形态——停止名恰一枚、发送名为零（不再并排双按钮）。
                Assert.Equal("停止请求", AutomationProperties.GetName(sendStop));
                Assert.Same(viewModel.StopCommand, sendStop.Command);
                Assert.True(sendStop.IsEnabled);
                Assert.Equal(
                    1,
                    FindVisualChildren<Button>(view)
                        .Count(button => AutomationProperties.GetName(button) == "停止请求"));
                Assert.DoesNotContain(
                    FindVisualChildren<Button>(view),
                    button => AutomationProperties.GetName(button) == "发送");
                var dangerBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    System.Windows.Application.Current.Resources["DangerBrush"]);
                var buttonBackground = Assert.IsType<System.Windows.Media.SolidColorBrush>(sendStop.Background);
                Assert.Equal(dangerBrush.Color, buttonBackground.Color);

                // 点击按钮当前绑定的停止命令 → 请求收束、按钮回发送态。
                sendStop.Command!.Execute(null);
                WaitFor(send, window);
                DoEvents(window);
                Assert.False(viewModel.IsSending);
                Assert.Equal("发送", AutomationProperties.GetName(sendStop));
                Assert.Same(viewModel.SendCommand, sendStop.Command);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
                window.Close();
            }
        });
    }

    /// <summary>
    /// P72 composer 改版：模型/权限收敛为输入框下方工具行的芯片（左侧加号旁
    /// 权限、右下模型，视觉树父级断言）；点开模型菜单选择条目仍回写 VM 的
    /// SelectedModel（功能不回归），选择后菜单自动收起。
    /// </summary>
    [Fact]
    public void ModelAndPermissionChipsSitInComposerToolRow()
    {
        RunOnSta(() =>
        {
            var first = new AiModelConfiguration(
                "first", "First", new AiProviderSettings { Model = "gpt-first" });
            var second = new AiModelConfiguration(
                "second", "Second", new AiProviderSettings { Model = "gpt-second" });
            var viewModel = new AssistantViewModel(
                new RuntimeConfirmationService(),
                modelCatalog: new AiModelCatalog([first, second], "first", first.Settings));
            var view = new AssistantView { DataContext = viewModel, Width = 1366, Height = 768 };
            var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                Border composer = Assert.IsType<Border>(view.FindName("ComposerPanel"));
                Button permissionChip = Assert.IsType<Button>(view.FindName("PermissionChip"));
                Button modelChip = Assert.IsType<Button>(view.FindName("ModelChip"));
                TextBox input = FindVisualChildren<TextBox>(view)
                    .Single(box => AutomationProperties.GetName(box) == "AI 消息");

                // 权限/模型芯片住进 composer 卡片，且位于输入框下方工具行。
                Assert.True(IsVisualDescendant(permissionChip, composer));
                Assert.True(IsVisualDescendant(modelChip, composer));
                Assert.True(IsVisualDescendant(input, composer));
                Point chipTopInInput = modelChip.TranslatePoint(new Point(0, 0), input);
                Assert.True(
                    chipTopInInput.Y >= input.ActualHeight - 0.01,
                    "工具行芯片应位于输入框下方。");

                // 功能不回归：点开模型菜单，选择第二项回写 SelectedModel 并收起菜单。
                var modelPopup = Assert.IsType<Popup>(view.FindName("ModelPopup"));
                modelPopup.IsOpen = true;
                DoEvents(window);
                ListBox modelMenu = FindVisualChildren<ListBox>(modelPopup.Child).Single();
                modelMenu.SelectedItem = second;
                DoEvents(window);
                Assert.Same(second, viewModel.SelectedModel);
                Assert.False(modelPopup.IsOpen, "选择条目后模型菜单应收起。");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P70-07 错误行渲染：发送失败插入消息流尾部的错误行——8px 红点（DangerBrush）
    /// + Danger 色 600 标题 + secondary 消息 + 等宽字体错误码（异常类型名）。
    /// </summary>
    [Fact]
    public void ErrorRowRendersRedDotTitleAndMonospaceCode()
    {
        RunOnSta(() =>
        {
            var viewModel = new AssistantViewModel(
                new RuntimeConfirmationService(),
                assistantClient: new CapturingFailingClient())
            {
                InputText = "必失败的问题",
            };
            var view = new AssistantView { DataContext = viewModel, Width = 1366, Height = 768 };
            var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                viewModel.SendAsync(CancellationToken.None).GetAwaiter().GetResult();
                DoEvents(window);

                Assert.Contains(viewModel.Messages, message => message.IsError);
                var dangerBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    System.Windows.Application.Current.Resources["DangerBrush"]);
                System.Windows.Shapes.Ellipse dot = FindVisualChildren<System.Windows.Shapes.Ellipse>(view)
                    .Single(ellipse => ellipse.IsVisible);
                var dotFill = Assert.IsType<System.Windows.Media.SolidColorBrush>(dot.Fill);
                Assert.Equal(dangerBrush.Color, dotFill.Color);

                TextBlock title = FindVisualChildren<TextBlock>(view)
                    .Single(block => block.IsVisible && block.Text == "发送失败");
                var titleBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(title.Foreground);
                Assert.Equal(dangerBrush.Color, titleBrush.Color);
                Assert.Equal(FontWeights.SemiBold, title.FontWeight);
                Assert.Contains(
                    FindVisualChildren<TextBlock>(view),
                    block => block.IsVisible && block.Text == "test failure");
                TextBlock code = FindVisualChildren<TextBlock>(view)
                    .Single(block => block.IsVisible && block.Text == "InvalidOperationException");
                Assert.StartsWith("Cascadia Mono", code.FontFamily.Source, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P70-07 附件文件卡：定宽 240 的卡片渲染文件名与 KB meta（"12.3 KB"），
    /// 移除按钮可点且点击后附件清空、卡片让位。
    /// </summary>
    [Fact]
    public void AttachmentCardsRenderWithSizeMetaAndRemoveButton()
    {
        RunOnSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), $"cccalendar-attach-{Guid.NewGuid():N}.md");
            // 12595 字节 → 12.3 KB：meta 断言要精确值，文件内容定长。
            File.WriteAllText(path, new string('#', 12595));
            try
            {
                var viewModel = new AssistantViewModel(new RuntimeConfirmationService());
                viewModel.AddAttachmentsAsync([path], CancellationToken.None).GetAwaiter().GetResult();
                var view = new AssistantView { DataContext = viewModel, Width = 1366, Height = 768 };
                var window = new Window { Content = view, Width = 1366, Height = 768, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    DoEvents(window);

                    // 文件卡：定宽 240 + 名称 + KB meta。
                    Border card = FindVisualChildren<Border>(view)
                        .Single(border => Math.Abs(border.Width - 240) < 0.01);
                    Assert.True(card.IsVisible);
                    Assert.Contains(
                        FindVisualChildren<TextBlock>(view),
                        block => block.IsVisible && block.Text == Path.GetFileName(path));
                    Assert.Contains(
                        FindVisualChildren<TextBlock>(view),
                        block => block.IsVisible && block.Text == "12.3 KB");

                    // 移除按钮：点击后附件清空、卡片让位。
                    Button remove = FindVisualChildren<Button>(view)
                        .Single(button => AutomationProperties.GetName(button) == "移除附件");
                    ClickButton(remove);
                    DoEvents(window);
                    Assert.Empty(viewModel.Attachments);
                    Assert.DoesNotContain(
                        FindVisualChildren<Border>(view),
                        border => Math.Abs(border.Width - 240) < 0.01);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    /// <summary>
    /// P70-04：按脚本回放 usage 的 fake 客户端——先发正文再发用量，
    /// 模拟一次带 usage 上报的模型子轮。
    /// </summary>
    private sealed class ScriptedUsageClient(params AiUsageUpdate[] usages) : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await onUpdate(new AiTextDelta("找到发布待办。"), cancellationToken);
            foreach (AiUsageUpdate usage in usages)
            {
                await onUpdate(usage, cancellationToken);
            }
        }
    }

    /// <summary>P74-03：图例四段名称。</summary>
    private static readonly string[] ContextMeterLegendNames =
        ["系统指令", "工具定义", "用户消息", "助手回复"];

    /// <summary>P74-03：先发上下文构成估算，再发文本。</summary>
    private sealed class CompositionClient(AiContextCompositionUpdate composition) : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await onUpdate(composition, cancellationToken);
            await onUpdate(new AiTextDelta("好的。"), cancellationToken);
        }
    }

    /// <summary>
    /// P70-06a：按钮走 Command 绑定时，RaiseEvent(ClickEvent) 只走路由事件、
    /// 不经过 OnClick 的 Command 执行；借 AutomationPeer 的 IInvokeProvider.
    /// Invoke 走完整 OnClick 管线（Click 事件 + Command），等价于真实点击。
    /// 注意 peer 的 Invoke 把点击异步投递到 DispatcherPriority.Input（防止
    /// 点击处理器弹对话框阻塞 UIA 调用方），因此要用同优先级的空回调把它
    /// 排干——DoEvents 泵的 Loaded/Render 优先级更高，永远轮不到 Input。
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

    /// <summary>P70-07：沿视觉树向上判断祖先（模型/权限控件是否住进 composer）。</summary>
    private static bool IsVisualDescendant(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = System.Windows.Media.VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    /// <summary>
    /// P70-07：泵消息循环等待异步任务收束——async 续体投递到 Dispatcher
    /// （Normal 优先级），STA 线程上 GetResult 会死锁，必须边泵边等。
    /// </summary>
    private static void WaitFor(Task task, Window window)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            DoEvents(window);
            Thread.Sleep(10);
        }

        Assert.True(task.IsCompleted, "异步任务在超时内未完成。");
    }

    /// <summary>
    /// P70-07：进入 StreamAsync 即挂起的 fake——维持 IsSending 流式态直到取消，
    /// 用于断言单按钮在流式/非流式之间的切换。
    /// </summary>
    private sealed class HeldStreamClient : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    /// <summary>P70-07：直接失败的 fake（走默认 StreamAsync 委托）——驱动错误行插入。</summary>
    private sealed class CapturingFailingClient : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            Task.FromException<AiAssistantResult>(new InvalidOperationException("test failure"));
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

    private sealed class RuntimeConfirmationService : IAiCreationConfirmationService
    {
        public AiCreationPreview Preview(AiCreationDraft draft) => new(
            Guid.NewGuid(),
            draft.Kind,
            draft.Title,
            [new AiPreviewField("类型", "待办"), new AiPreviewField("内容", draft.Content ?? "")]);

        public Task<AiCreationResult> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCreationResult(Guid.NewGuid(), AiCreationKind.Todo));

        public void Cancel(Guid proposalId)
        {
        }
    }
}
