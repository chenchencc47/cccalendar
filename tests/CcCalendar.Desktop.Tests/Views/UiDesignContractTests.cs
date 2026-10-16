using System.Globalization;
using System.IO;

namespace CcCalendar.Desktop.Tests.Views;

public sealed class UiDesignContractTests
{
    [Fact]
    public void ThemeStylesCoverEveryCommonWorkspaceControl()
    {
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");
        string[] requiredControls =
        [
            "TextBox",
            "ComboBox",
            "CheckBox",
            "DatePicker",
            "ProgressBar",
            "ListBox",
            "ListBoxItem",
            "TabControl",
            "TabItem",
        ];

        foreach (string control in requiredControls)
        {
            Assert.Contains($"TargetType=\"{control}\"", theme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HorizontalScrollBarKeepsLogicalLeftToRightDirection()
    {
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");
        int horizontalTrigger = theme.IndexOf(
            "<Trigger Property=\"Orientation\" Value=\"Horizontal\">",
            StringComparison.Ordinal);
        Assert.True(horizontalTrigger >= 0);
        int triggerEnd = theme.IndexOf("</Trigger>", horizontalTrigger, StringComparison.Ordinal);
        Assert.True(triggerEnd > horizontalTrigger);
        string horizontalTemplate = theme[horizontalTrigger..triggerEnd];
        Assert.Contains(
            "Property=\"IsDirectionReversed\" Value=\"False\"",
            horizontalTemplate,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ComboBoxTemplateSupportsMouseToggleAndEditableInput()
    {
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");

        Assert.Contains(
            "IsChecked=\"{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}\"",
            theme,
            StringComparison.Ordinal);
        Assert.Contains("ClickMode=\"Press\"", theme, StringComparison.Ordinal);
        Assert.Contains("PART_EditableTextBox", theme, StringComparison.Ordinal);
    }

    [Fact]
    public void MainSearchHasAVisibleWatermark()
    {
        string mainWindow = ReadWorkspaceFile("src", "CcCalendar.Desktop", "MainWindow.xaml");

        Assert.Contains(
            "Text=\"搜索项目、日程、待办和记录\"",
            mainWindow,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// P70-06b 主界面收起契约：主导航 rail 化由 IsNavCollapsed 驱动（条目文字
    /// 隐藏 + rail 工具提示 + 收起/展开按钮），顶部搜索栏由 IsTopSearchCollapsed
    /// 驱动（收起后原位留搜索图标按钮）。
    /// </summary>
    [Fact]
    public void MainWindowExposesNavRailAndTopSearchCollapseContract()
    {
        string mainWindow = ReadWorkspaceFile("src", "CcCalendar.Desktop", "MainWindow.xaml");

        // 主导航 rail：IsNavCollapsed 双态 + 切换按钮。
        Assert.Contains("IsNavCollapsed", mainWindow, StringComparison.Ordinal);
        Assert.Contains("收起导航栏", mainWindow, StringComparison.Ordinal);
        Assert.Contains("展开导航栏", mainWindow, StringComparison.Ordinal);
        // rail 态条目文字隐藏后靠 ToolTip 补全条目名。
        Assert.Contains("ToolTip=\"{Binding Label}\"", mainWindow, StringComparison.Ordinal);
        // 顶部搜索栏：IsTopSearchCollapsed 双态 + 收起/展开按钮。
        Assert.Contains("IsTopSearchCollapsed", mainWindow, StringComparison.Ordinal);
        Assert.Contains("收起搜索栏", mainWindow, StringComparison.Ordinal);
        Assert.Contains("展开搜索栏", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolCenterUsesFocusedCategoriesInsteadOfOneTallCanvas()
    {
        string toolsView = ReadWorkspaceFile(
            "src",
            "CcCalendar.Desktop",
            "Views",
            "ToolsView.xaml");

        Assert.DoesNotContain("MinHeight=\"1040\"", toolsView, StringComparison.Ordinal);
        Assert.Contains("Header=\"时间工具\"", toolsView, StringComparison.Ordinal);
        Assert.Contains("Header=\"专注\"", toolsView, StringComparison.Ordinal);
        Assert.Contains("Header=\"截图与贴屏\"", toolsView, StringComparison.Ordinal);
        Assert.Contains("Header=\"剪贴板\"", toolsView, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddWindowBindsDateAndTimeSelections()
    {
        string quickAdd = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml");

        Assert.Contains(
            "SelectedDate=\"{Binding SelectedDate}\"",
            quickAdd,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectedItem=\"{Binding SelectedStartTime}\"",
            quickAdd,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectedItem=\"{Binding SelectedEndTime}\"",
            quickAdd,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsExposeUpdateCheckAndColorPalette()
    {
        string settings = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "SettingsView.xaml");

        Assert.Contains("Click=\"CheckForUpdatesClick\"", settings, StringComparison.Ordinal);
        Assert.Contains("TogglePaletteCommand", settings, StringComparison.Ordinal);
        Assert.Contains("PaletteOptions", settings, StringComparison.Ordinal);
        Assert.Contains("ColorStringToBrushConverter", settings, StringComparison.Ordinal);
        Assert.Contains("PlacementTarget.DataContext", settings, StringComparison.Ordinal);
        Assert.Contains("Background=\"{Binding Color, Converter={StaticResource ColorStringToBrushConverter}}\"", settings, StringComparison.Ordinal);
        Assert.Contains("Click=\"CustomColorClick\"", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowExposesUpdateIconBesideQuickAdd()
    {
        string mainWindow = ReadWorkspaceFile("src", "CcCalendar.Desktop", "MainWindow.xaml");

        Assert.Contains("AutomationProperties.Name=\"下载更新\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource PrimaryButtonStyle}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsUpdateAvailable", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Click=\"UpdateClick\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("点击下载并安装", mainWindow, StringComparison.Ordinal);
        string appCode = ReadWorkspaceFile("src", "CcCalendar.Desktop", "App.xaml.cs");
        Assert.Contains("更新内容：", appCode, StringComparison.Ordinal);
        Assert.Contains("update.ReleaseNotes", appCode, StringComparison.Ordinal);
        Assert.Contains("Material Symbols download asset", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateDownloadRequiresExplicitUserConfirmation()
    {
        string appCode = ReadWorkspaceFile("src", "CcCalendar.Desktop", "App.xaml.cs");
        string manifest = ReadWorkspaceFile("version.json");

        Assert.Contains("_ = CheckForUpdatesAsync();", appCode, StringComparison.Ordinal);
        Assert.Contains("mainWindow?.SetAvailableUpdate(update);", appCode, StringComparison.Ordinal);
        Assert.Contains("if (confirmation != MessageBoxResult.Yes)", appCode, StringComparison.Ordinal);
        Assert.Contains("DownloadInstallerAsync(update", appCode, StringComparison.Ordinal);
        Assert.Contains("\"mandatory\": false", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void TrayExitHidesIconAndIgnoresLateCallbacks()
    {
        string appCode = ReadWorkspaceFile("src", "CcCalendar.Desktop", "App.xaml.cs");
        string trayCode = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Desktop", "TrayIconService.cs");

        Assert.Contains("trayIcon?.Hide();", appCode, StringComparison.Ordinal);
        Assert.Contains("if (isExiting)", appCode, StringComparison.Ordinal);
        Assert.Contains("public void Hide()", trayCode, StringComparison.Ordinal);
        Assert.Contains("if (isDisposed)", trayCode, StringComparison.Ordinal);
        Assert.Contains("WaitAsync(TimeSpan.FromSeconds(2))", appCode, StringComparison.Ordinal);
        Assert.Contains("cccalendar-single-instance", appCode, StringComparison.Ordinal);
        Assert.Contains("if (!ownsSingleInstanceMutex)", appCode, StringComparison.Ordinal);
    }

    /// <summary>
    /// P70-06a 助理页左侧会话栏契约（语义移植 deepseek-harness 侧边栏折叠交互）：
    /// 左列会话栏（展开 240 / 收起 rail 48）+「新对话」主按钮 + 条目
    /// （标题 + 相对时间 + hover 删除）+ 空态引导；视觉复用主导航条目样式
    /// （NavigationItemStyle 的标记条/AccentSubtle 选中底/hover 动画语法）。
    /// 顶部会话 ComboBox 被会话栏替代。
    /// </summary>
    [Fact]
    public void AssistantShowsSessionsPaneWithCollapseRail()
    {
        string assistant = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        // 会话栏结构：新对话主按钮、收起/展开切换（两个状态都可达）、条目数据。
        Assert.Contains("AutomationProperties.Name=\"新对话\"", assistant, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"收起会话栏\"", assistant, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"展开会话栏\"", assistant, StringComparison.Ordinal);
        Assert.Contains("ToggleSessionsPaneCommand", assistant, StringComparison.Ordinal);
        // P73：开合状态由 code-behind 动画驱动，XAML 不再持有 IsSessionsPaneCollapsed
        // 的 DataTrigger（存在性断言移到下方 code-behind 段）。
        Assert.Contains("{Binding Sessions}", assistant, StringComparison.Ordinal);
        // 前缀匹配：XAML 为 SelectedItem="{Binding CurrentSession, Mode=TwoWay}"，
        // TwoWay 是「点条目切会话」回写 CurrentSession 的前提。
        Assert.Contains("{Binding CurrentSession", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding RelativeTimeText}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding HasSessionHistory", assistant, StringComparison.Ordinal);
        Assert.Contains("DeleteChatCommand", assistant, StringComparison.Ordinal);
        // 视觉语言与主导航一致：复用 NavigationItemStyle 条目样式（标记条 + 选中底）。
        Assert.Contains("NavigationItemStyle", assistant, StringComparison.Ordinal);
        // 空态引导文案。
        Assert.Contains("暂无历史会话", assistant, StringComparison.Ordinal);
        // 顶部会话 ComboBox 已被会话栏替代：按旧控件的无障碍名精确排除
        // （不能断言不含"历史会话"——空态文案"暂无历史会话"天然包含该子串）。
        Assert.DoesNotContain("AutomationProperties.Name=\"历史会话\"", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("切换历史对话", assistant, StringComparison.Ordinal);

        // 列宽切换（240 ↔ 48）在 code-behind 驱动：ColumnDefinition.Width 不是
        // 可绑定属性，DataTrigger 无法跨模板 Setter 命名列定义。P73 起切换走
        // GridLengthAnimation 平滑补间（见 AssistantP73…测试）。
        string codeBehind = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml.cs");
        Assert.Contains("IsSessionsPaneCollapsed", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinition", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>
    /// P73（用户反馈三项）：
    /// ① 消息流内组件去白底去描边——正文背景非白，图标按钮/胶囊须透明
    ///    跟随容器底色（IconButtonStyle 透明+无边框、StatPill 去发丝边、
    ///    复制消息按钮改用 IconButtonStyle）；
    /// ② 会话面板与主导航之间无缝隙——面板左边距 24→0，直接贴住导航右缘；
    /// ③ 开合平滑动画——GridLengthAnimation 补间列宽，可见性终态由动画
    ///    Completed 落定，XAML 不再用 DataTrigger 直跳。
    /// </summary>
    [Fact]
    public void AssistantP73BorderlessChipsSeamlessPaneAndAnimatedCollapse()
    {
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");
        string assistant = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");
        string codeBehind = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml.cs");

        // ① 图标按钮：透明底 + 无描边。
        int iconStyleStart = theme.IndexOf("x:Key=\"IconButtonStyle\"", StringComparison.Ordinal);
        Assert.True(iconStyleStart >= 0);
        string iconStyle = theme.Substring(iconStyleStart, theme.IndexOf("</Style>", iconStyleStart, StringComparison.Ordinal) - iconStyleStart);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", iconStyle, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", iconStyle, StringComparison.Ordinal);

        // ① 统计胶囊：去发丝边（无 BorderBrush setter；模板内的
        // TemplateBinding 残留无害——Thickness=0 时不再绘制）。
        int pillStyleStart = theme.IndexOf("x:Key=\"StatPillButtonStyle\"", StringComparison.Ordinal);
        Assert.True(pillStyleStart >= 0);
        string pillStyle = theme.Substring(pillStyleStart, theme.IndexOf("</Style>", pillStyleStart, StringComparison.Ordinal) - pillStyleStart);
        Assert.DoesNotContain("Property=\"BorderBrush\"", pillStyle, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", pillStyle, StringComparison.Ordinal);

        // ① 复制消息按钮改用 IconButtonStyle（不再吃隐式样式的白底描边）。
        int copyIndex = assistant.IndexOf("AutomationProperties.Name=\"复制消息\"", StringComparison.Ordinal);
        Assert.True(copyIndex >= 0);
        string copyRegion = assistant.Substring(copyIndex, Math.Min(300, assistant.Length - copyIndex));
        Assert.Contains("Style=\"{StaticResource IconButtonStyle}\"", copyRegion, StringComparison.Ordinal);

        // ② 会话面板贴住导航栏与顶栏：左、上边距 0，仅留底部呼吸。
        Assert.Contains("x:Name=\"SessionsPanel\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,0,24\"", assistant, StringComparison.Ordinal);

        // ③ 开合动画：补间基建 + code-behind 驱动 + DataTrigger 直跳禁令。
        Assert.Contains("GridLengthAnimation", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetSessionsPaneState", codeBehind, StringComparison.Ordinal);
        Assert.Contains("PaneAnimationEnabled", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DataTrigger Binding=\"{Binding IsSessionsPaneCollapsed}\"",
            assistant,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddIsShownAsNonModalWindow()
    {
        string mainWindow = ReadWorkspaceFile("src", "CcCalendar.Desktop", "MainWindow.xaml.cs");
        string quickAdd = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml.cs");

        Assert.Contains("dialog.Show();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("dialog.Closed", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Close();", quickAdd, StringComparison.Ordinal);
        Assert.DoesNotContain("DialogResult = true", quickAdd, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantMessagesExposeCollapsibleThinkingRow()
    {
        string assistant = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        Assert.Contains("CornerRadius=\"18\"", assistant, StringComparison.Ordinal);
        // P70-02：思考块从 Expander + SubtleSurface 背景块改为一行式折叠行
        // （harness ReasoningRow 语义）：灯泡 + 派生标题 + 旋转 Chevron，整行可点击。
        Assert.DoesNotContain("<Expander", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"思考\"", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding ThinkingTitle}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding ThinkingText}", assistant, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"思考过程\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Kind=\"Lightbulb\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Kind=\"ChevronDown\"", assistant, StringComparison.Ordinal);
        // 流式思考中的 shimmer：OpacityMask 三档渐变 + 永续扫光 Storyboard，
        // 由 IsReasoningStreaming 驱动启停。
        Assert.Contains("IsReasoningStreaming", assistant, StringComparison.Ordinal);
        Assert.Contains("ThinkingShimmer", assistant, StringComparison.Ordinal);
        Assert.Contains("RepeatBehavior=\"Forever\"", assistant, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly=\"True\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text, Mode=OneWay}\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Click=\"CopyMessageClick\"", assistant, StringComparison.Ordinal);
        // P70-01：消息形态改为聊天客户端形态。用户消息是右对齐气泡
        // （BubbleBrush 底 + RadiusBubble 圆角 + 上下10左右16 内边距），
        // 头像/名称行（"AI"/"你"字样）随评论区形态一并移除。
        Assert.Contains("Background=\"{DynamicResource BubbleBrush}\"", assistant, StringComparison.Ordinal);
        Assert.Contains("CornerRadius=\"{StaticResource RadiusBubble}\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Padding=\"16,10\"", assistant, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Right\"", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"AI\"", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"你\"", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("ThinkEnabled", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("DeepSearchEnabled", assistant, StringComparison.Ordinal);
    }

    /// <summary>
    /// P70-05 工具调用行契约（语义移植 deepseek-harness ToolRow）：
    /// 消息模板第三个分支（IsToolCall 触发器）= 8px 状态点
    /// （Running=呼吸动画 / Ok=SuccessBrush / Error=DangerBrush）
    /// + 工具中文名 + 参数摘要 + 旋转 Chevron；展开区是等宽 ToolDetail。
    /// </summary>
    [Fact]
    public void AssistantMessagesExposeToolCallRow()
    {
        string assistant = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        // 第三个分支由 IsToolCall 数据触发器驱动。
        Assert.Contains("IsToolCall", assistant, StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding ToolDisplayName}\"",
            assistant,
            StringComparison.Ordinal);
        Assert.Contains("{Binding ToolDisplayName}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding ToolSummary}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding ToolDetail}", assistant, StringComparison.Ordinal);
        // 状态点：Ellipse + Running 呼吸动画 + Ok/Error 语义画刷换色。
        Assert.Contains("ToolStateDot", assistant, StringComparison.Ordinal);
        Assert.Contains("ToolRunningPulse", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding ToolState}", assistant, StringComparison.Ordinal);
        Assert.Contains("SuccessBrush", assistant, StringComparison.Ordinal);
        Assert.Contains("DangerBrush", assistant, StringComparison.Ordinal);
        // 展开区：等宽字体（与统计弹窗同一字体栈）+ SubtleSurface 底。
        Assert.Contains("FontFamily=\"Cascadia Mono, Microsoft YaHei UI\"", assistant, StringComparison.Ordinal);
        Assert.Contains("SubtleSurfaceBrush", assistant, StringComparison.Ordinal);
    }

    /// <summary>
    /// P70-04 统计胶囊契约（语义移植 deepseek-harness StatsPills）：
    /// composer 按钮行正下方有两枚胶囊（会话统计 / Token 用量），各有 Popup 详情；
    /// 助手消息尾部有轮次用量胶囊（Usage 为空时隐藏）。
    /// Q5 裁决：只显示 tokens，不做成本/金额。
    /// </summary>
    [Fact]
    public void AssistantComposerShowsStatsPillsWithDetailPopups()
    {
        string assistant = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        Assert.Contains("AutomationProperties.Name=\"会话统计\"", assistant, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Token 用量\"", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding SessionStatsSummary}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding TokenStatsSummary}", assistant, StringComparison.Ordinal);
        // 整行胶囊由 HasUsageData 控制可见性：无用量数据不占位。
        Assert.Contains("HasUsageData", assistant, StringComparison.Ordinal);
        Assert.Contains("Kind=\"Database\"", assistant, StringComparison.Ordinal);
        Assert.Contains("StatPillButtonStyle", assistant, StringComparison.Ordinal);
        // 两枚胶囊各带一个 Popup 详情（StaysOpen=False 点击外部关闭）。
        Assert.Contains("StaysOpen=\"False\"", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding SessionStatRows}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding TokenStatRows}", assistant, StringComparison.Ordinal);
        // 助手消息尾部的轮次用量胶囊。
        Assert.Contains("{Binding UsageSummary}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding UsageDetail}", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding HasUsage,", assistant, StringComparison.Ordinal);

        // 胶囊样式在主题里集中声明；只显示 tokens，不出现金额/成本字样（Q5 裁决）。
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");
        Assert.Contains("x:Key=\"StatPillButtonStyle\"", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("¥", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("$", assistant, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddInvitationUsesPreviewEnterForImport()
    {
        string quickAdd = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml");

        Assert.Contains("PreviewKeyDown=\"MeetingInvitationKeyDown\"", quickAdd, StringComparison.Ordinal);
        Assert.DoesNotContain("\n                                 KeyDown=\"MeetingInvitationKeyDown\"", quickAdd, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddWindowOffersMeetingRoomSelection()
    {
        string quickAdd = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml");

        Assert.Contains("RoomBox", quickAdd, StringComparison.Ordinal);
        Assert.Contains(
            "ItemsSource=\"{Binding RoomOptions}\"",
            quickAdd,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectedItem=\"{Binding SelectedRoom}\"",
            quickAdd,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddWindowOffersWeeklyRepeatSelection()
    {
        string quickAdd = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml");

        Assert.Contains("Text=\"重复\"", quickAdd, StringComparison.Ordinal);
        foreach (string day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
        {
            Assert.Contains($"x:Name=\"Repeat{day}\"", quickAdd, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TodoDragStartIsWiredAtViewRootNotOnCards()
    {
        string todoView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "TodoView.xaml");

        // 拖动启动必须挂在根元素：MouseMove 只派发给光标下方元素，
        // 挂在卡片上时鼠标快速移出卡片后拖动永远无法启动。
        Assert.Contains(
            "PreviewMouseMove=\"RootPreviewMouseMove\"",
            todoView,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TodoPreviewMouseMove",
            todoView,
            StringComparison.Ordinal);
        // 卡片仍需记录按下起点。
        Assert.Contains(
            "TodoCardPreviewMouseLeftButtonDown",
            todoView,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EventEditWindowShowsRoomBookingPickerBesideForm()
    {
        string editWindow = ReadWorkspaceFile("src", "CcCalendar.Desktop", "EventEditWindow.xaml");

        Assert.Contains(
            "<views:RoomBookingPicker x:Name=\"Picker\"",
            editWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "Picked=\"PickerPicked\"",
            editWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "DateSwitched=\"PickerDateSwitched\"",
            editWindow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddWindowEmbedsRoomBookingPicker()
    {
        string quickAdd = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml");

        Assert.Contains(
            "<views:RoomBookingPicker x:Name=\"Picker\"",
            quickAdd,
            StringComparison.Ordinal);
        Assert.Contains(
            "Picked=\"PickerPicked\"",
            quickAdd,
            StringComparison.Ordinal);
        Assert.Contains(
            "DateSwitched=\"PickerDateSwitched\"",
            quickAdd,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DayScheduleWindowSupportsViewAndDoubleClickEdit()
    {
        string dayWindow = ReadWorkspaceFile("src", "CcCalendar.Desktop", "DayScheduleWindow.xaml");

        Assert.Contains("双击日程可编辑", dayWindow, StringComparison.Ordinal);
        Assert.Contains("编辑日程", dayWindow, StringComparison.Ordinal);
        Assert.Contains(
            "MouseLeftButtonDown=\"EventMouseLeftButtonDown\"",
            dayWindow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MeetingDetailsShowsOrganizerName()
    {
        string code = ReadWorkspaceFile("src", "CcCalendar.Desktop", "MeetingDetailsWindow.xaml.cs");

        Assert.Contains("会议人员", code, StringComparison.Ordinal);
        Assert.Contains("OrganizerName", code, StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarViewOffersRoomTimelineMode()
    {
        string calendar = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "CalendarView.xaml");

        Assert.Contains("Content=\"会议室\"", calendar, StringComparison.Ordinal);
        Assert.Contains(
            "ItemsSource=\"{Binding RoomColumns}\"",
            calendar,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TodoViewSupportsTypeToCreateAndClickToToggle()
    {
        string todoView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "TodoView.xaml");

        Assert.Contains(
            "KeyDown=\"NewTodoKeyDown\"",
            todoView,
            StringComparison.Ordinal);
        Assert.Contains(
            "MouseLeftButtonUp=\"TodoCardMouseLeftButtonUp\"",
            todoView,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarWorkspaceIncludesSelectedDateDetailsPane()
    {
        string calendarView = ReadWorkspaceFile(
            "src",
            "CcCalendar.Desktop",
            "Views",
            "CalendarView.xaml");

        Assert.Contains("Text=\"{Binding SelectedDateText}\"", calendarView, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding SelectedEventDetails}\"", calendarView, StringComparison.Ordinal);
    }

    [Fact]
    public void TodayWorkspaceKeepsDailyWorkAheadOfWeatherDetails()
    {
        string todayView = ReadWorkspaceFile(
            "src",
            "CcCalendar.Desktop",
            "Views",
            "TodayView.xaml");

        int workArea = todayView.IndexOf("x:Name=\"TodayWorkArea\"", StringComparison.Ordinal);
        int weatherDetails = todayView.IndexOf("x:Name=\"WeatherDetails\"", StringComparison.Ordinal);

        Assert.True(workArea >= 0);
        Assert.True(weatherDetails > workArea);
    }

    [Fact]
    public void PlanningWorkspacesAvoidNestedPageBackgroundCards()
    {
        string projectView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "ProjectView.xaml");
        string todoView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "TodoView.xaml");

        Assert.DoesNotContain("Background=\"{StaticResource PageBackgroundBrush}\"", projectView, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"{StaticResource PageBackgroundBrush}\"", todoView, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectAndRecordActionsExposeClickHandlers()
    {
        string projectView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "ProjectView.xaml");
        string recordView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "RecordView.xaml");

        Assert.Contains("Click=\"CreateProjectClick\"", projectView, StringComparison.Ordinal);
        Assert.Contains("Click=\"CreateRecordClick\"", recordView, StringComparison.Ordinal);
        Assert.Contains("Click=\"SaveClick\"", recordView, StringComparison.Ordinal);
        Assert.Contains("Click=\"DeleteProjectClick\"", projectView, StringComparison.Ordinal);
        Assert.Contains("Text=\"搜索记录\"", recordView, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SaveStatusText\"", recordView, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantMessageListUsesPixelScrolling()
    {
        string assistantView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        Assert.Contains("ScrollViewer.CanContentScroll=\"False\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.IsVirtualizing=\"False\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseWheel=\"MessageListPreviewMouseWheel\"", assistantView, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddInvitationIsEventOnly()
    {
        string quickAddView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml");
        string quickAddCode = ReadWorkspaceFile("src", "CcCalendar.Desktop", "QuickAddWindow.xaml.cs");

        Assert.Contains("x:Name=\"MeetingInvitationFields\"", quickAddView, StringComparison.Ordinal);
        Assert.Contains("MeetingInvitationFields.Visibility = visibility", quickAddCode, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MeetingReminderCheckBox\"", quickAddView, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MeetingReminderMinutesBox\"", quickAddView, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MeetingReminderCheckBox\"", ReadWorkspaceFile("src", "CcCalendar.Desktop", "EventEditWindow.xaml"), StringComparison.Ordinal);
    }

    [Fact]
    public void ToolsExposeCustomTimerDurationsAndReminderInput()
    {
        string toolsView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "ToolsView.xaml");
        string settingsView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "SettingsView.xaml");

        Assert.Contains("Binding CountdownMinutes", toolsView, StringComparison.Ordinal);
        Assert.Contains("Binding PomodoroFocusMinutes", toolsView, StringComparison.Ordinal);
        Assert.Contains("Binding PomodoroBreakMinutes", toolsView, StringComparison.Ordinal);
        Assert.Contains("Binding DefaultSnoozeMinutes", settingsView, StringComparison.Ordinal);
        Assert.Contains("TextBox Grid.Column=\"1\"", settingsView, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantSeparatesCommandsFromModelAndPermissionContext()
    {
        string assistantView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        Assert.Contains("x:Name=\"AssistantCommandBar\"", assistantView, StringComparison.Ordinal);
        // P72：模型/权限从顶部 ContextBar 收敛为 composer 工具行芯片
        // （加号右侧权限下拉 + 右下模型菜单），声明位置在消息列表之后，顶部只留命令按钮。
        Assert.DoesNotContain("x:Name=\"AssistantContextBar\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PermissionChip\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ModelChip\"", assistantView, StringComparison.Ordinal);
        Assert.True(
            assistantView.IndexOf("x:Name=\"PermissionChip\"", StringComparison.Ordinal)
                > assistantView.IndexOf("x:Name=\"MessageList\"", StringComparison.Ordinal),
            "权限芯片应位于 composer 工具行（消息列表声明之后），不再占顶部一行。");
    }

    /// <summary>
    /// P70-07 Composer 精化契约：发送/停止合并为单按钮（Style 触发器切换
    /// Command/自动化名/Danger 底）、错误行（IsError 第四分支 + DangerBrush 红点 +
    /// 标题/消息/等宽错误码绑定）、附件文件卡（定宽 240 + KB meta 转换器 + 移除按钮）。
    /// </summary>
    [Fact]
    public void AssistantComposerMergesSendStopAndShowsErrorRowAndFileCards()
    {
        string assistantView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        // 单按钮两态：默认 SendCommand，IsSending 触发器切 StopCommand + 停止名 + Danger 底。
        Assert.Contains("x:Name=\"SendStopButton\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding SendCommand}\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding StopCommand}\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("Binding IsSending", assistantView, StringComparison.Ordinal);
        Assert.Contains("Value=\"停止请求\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("Value=\"发送\"", assistantView, StringComparison.Ordinal);
        // 旧并排双按钮移除：发送/停止不再以独立按钮的属性绑定形式出现。
        Assert.DoesNotContain("Command=\"{Binding SendCommand}\"", assistantView, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding StopCommand}\"", assistantView, StringComparison.Ordinal);

        // 错误行：IsError 分支 + 红点 + 标题/消息/错误码绑定 + Danger 画刷。
        Assert.Contains("Binding IsError", assistantView, StringComparison.Ordinal);
        Assert.Contains("{Binding ErrorTitle}", assistantView, StringComparison.Ordinal);
        Assert.Contains("{Binding ErrorMessage}", assistantView, StringComparison.Ordinal);
        Assert.Contains("{Binding ErrorCode}", assistantView, StringComparison.Ordinal);
        Assert.Contains("Fill=\"{DynamicResource DangerBrush}\"", assistantView, StringComparison.Ordinal);

        // 文件卡：定宽 240 + 移除附件 + KB meta 转换器。
        Assert.Contains("Width=\"240\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"移除附件\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("FileSizeTextConverter", assistantView, StringComparison.Ordinal);
    }

    /// <summary>
    /// P72（用户裁决）：消息区与输入卡均全幅铺开，不套 harness 的居中
    /// 宽度轴；用户气泡右对齐贴齐列表右缘，上限 = 列表宽 × 0.7
    /// （源绑 ListBox.ActualWidth，独立布局输出）。
    /// 气泡宽度禁止绑 MessageRoot（ActualWidth/MaxWidth 均为循环依赖，
    /// P72 事故：气泡卡死 0 宽、提问不显示）。
    /// </summary>
    [Fact]
    public void AssistantUserBubbleAlignsRightAndComposerSpansFullWidth()
    {
        string assistantView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");
        string appXaml = ReadWorkspaceFile("src", "CcCalendar.Desktop", "App.xaml");

        // 用户气泡：右对齐 + 列表宽 × 0.7 上限（独立源 ListBox.ActualWidth）。
        Assert.Contains(
            "MaxWidth=\"{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}, Converter={StaticResource ScaleClampConverter}, ConverterParameter='0.7'}\"",
            assistantView,
            StringComparison.Ordinal);
        Assert.Contains(
            "Background=\"{DynamicResource BubbleBrush}\"",
            assistantView,
            StringComparison.Ordinal);
        // 循环依赖禁令：不得绑模板根 MessageRoot 的任何宽度属性。
        Assert.DoesNotContain(
            "ElementName=MessageRoot",
            assistantView,
            StringComparison.Ordinal);
        // 全幅裁决：居中宽度轴（clamp 参数）不得回流消息列或输入卡。
        Assert.DoesNotContain(
            "ConverterParameter='0.64;680;920",
            assistantView,
            StringComparison.Ordinal);
        Assert.Contains(
            "ScaleClampConverter x:Key=\"ScaleClampConverter\"",
            appXaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantActionsExposeAccessibleNames()
    {
        string assistantView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        Assert.Contains("AutomationProperties.Name=\"复制消息\"", assistantView, StringComparison.Ordinal);
        // P70-07：停止请求名随发送/停止合并移入单按钮的 Style 切换值
        // （见 AssistantComposerMergesSendStopAndShowsErrorRowAndFileCards）。
        Assert.Contains("AutomationProperties.Name=\"移除附件\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"确认创建提案\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"确认变更提案\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"寻找空闲时间\"", assistantView, StringComparison.Ordinal);
    }

    [Fact]
    public void MousePassthroughRecoveryIsDiscoverable()
    {
        string tray = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Desktop", "TrayIconService.cs");
        string settings = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "SettingsView.xaml");

        Assert.Contains("恢复所有桌面组件鼠标操作（Ctrl+Alt+Shift+P）", tray, StringComparison.Ordinal);
        Assert.Contains("关闭全部鼠标穿透", settings, StringComparison.Ordinal);
        Assert.Contains("Ctrl + Alt + Shift + P", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsExposeReleaseNotesAction()
    {
        string settingsView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "SettingsView.xaml");
        string settingsCode = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "SettingsView.xaml.cs");

        Assert.Contains("查看更新内容", settingsView, StringComparison.Ordinal);
        Assert.Contains("ReleaseNotesClick", settingsView, StringComparison.Ordinal);

        // P67：更新说明改为从公网清单的 releaseNotes 读取，不再写死在代码里
        // （原先的写死常量会让用户点开看到的是 0.6.5 的旧说明）。
        Assert.Contains("FetchReleaseNotesAsync", settingsCode, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentReleaseNotes", settingsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyProgressValuesUseOneWayBindings()
    {
        string todoView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "TodoView.xaml");
        string toolsView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "ToolsView.xaml");

        Assert.Contains(
            "Value=\"{Binding ProgressPercent, Mode=OneWay}\"",
            todoView,
            StringComparison.Ordinal);
        Assert.Contains(
            "Value=\"{Binding Progress.DayPercent, Mode=OneWay}\"",
            toolsView,
            StringComparison.Ordinal);
        Assert.Contains(
            "Value=\"{Binding Progress.WeekPercent, Mode=OneWay}\"",
            toolsView,
            StringComparison.Ordinal);
        Assert.Contains(
            "Value=\"{Binding Progress.MonthPercent, Mode=OneWay}\"",
            toolsView,
            StringComparison.Ordinal);
        Assert.Contains(
            "Value=\"{Binding Progress.YearPercent, Mode=OneWay}\"",
            toolsView,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// P60-05：页面标题必须复用 <c>PageTitleStyle</c>，不能在页面内重新定义同义尺寸。
    ///
    /// UI_DESIGN §10 要求「页面标题、区块标题、辅助文字和图标按钮分别复用
    /// PageTitleStyle/SectionTitleStyle/CaptionTextStyle/IconButtonStyle」。
    /// 修复前有 10 处页面标题直接写 <c>FontSize="20" FontWeight="SemiBold"</c>，
    /// 一旦令牌调整，这些位置会与规范漂移。
    /// </summary>
    [Fact]
    public void PageTitlesUseTheSharedStyleInsteadOfInlineLiterals()
    {
        string[] views =
        [
            "MainWindow.xaml",
            "QuickAddWindow.xaml",
            "QuickPanelWindow.xaml",
            "EventEditWindow.xaml",
            "MeetingDetailsWindow.xaml",
            "MeetingExportWindow.xaml",
            "DayScheduleWindow.xaml",
            "DesktopComponentWindow.xaml",
            "DesktopWorkbenchWindow.xaml",
            "Views/AssistantView.xaml",
            "Views/CalendarView.xaml",
            "Views/ProjectView.xaml",
            "Views/RecordView.xaml",
            "Views/SettingsView.xaml",
            "Views/StatisticsView.xaml",
            "Views/TodayView.xaml",
            "Views/TodoView.xaml",
            "Views/ToolsView.xaml",
        ];

        var offenders = new List<string>();
        foreach (string view in views)
        {
            string[] segments = ["src", "CcCalendar.Desktop", .. view.Split('/')];
            string content = ReadWorkspaceFile(segments);
            string fileName = segments[^1];

            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(
                    content,
                    "<TextBlock[^>]*FontSize=\"20\"[^>]*FontWeight=\"SemiBold\"[^>]*>",
                    System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                string literal = match.Value;

                // 天气摘要用时钟级字号，是刻意的非页面标题用法（UI_DESIGN §2.1
                // 允许时钟与天气标题使用较大字号），显式放行。
                if (literal.Contains("Weather.CurrentSummary", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{fileName}: {literal.Trim()}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "以下页面标题未复用 PageTitleStyle，而是内联了 FontSize=\"20\"+SemiBold：\n"
                + string.Join("\n", offenders));
    }

    /// <summary>
    /// P60-05：助理导航不得使用星光图标（UI_DESIGN §1.6「AI 降权：不使用星光图标」）。
    /// </summary>
    [Fact]
    public void AssistantNavigationAvoidsSparkleIconography()
    {
        string navigation = ReadWorkspaceFile(
            "src", "CcCalendar.Desktop", "ViewModels", "MainWindowViewModel.cs");

        Assert.DoesNotContain("PackIconLucideKind.Sparkles", navigation, StringComparison.Ordinal);
    }

    /// <summary>
    /// P60-05：区域选择遮罩必须走主题令牌而不是硬编码半透明黑。
    /// </summary>
    [Fact]
    public void RegionSelectorScrimUsesTheThemeToken()
    {
        string regionSelector = ReadWorkspaceFile(
            "src", "CcCalendar.Desktop", "RegionSelectorWindow.xaml");

        Assert.DoesNotContain("#33000000", regionSelector, StringComparison.Ordinal);
        Assert.Contains("OverlayScrimBrush", regionSelector, StringComparison.Ordinal);
    }

    /// <summary>
    /// P65：页面级留白必须统一，不能在页面之间各写一套。
    ///
    /// 修复前实测 4 种值并存：`24,20,24,24`（7 页）、`28,24,28,28`（助手页）、
    /// `24,20,32,32`（设置页 5 处）、`22,18,22,16`（日程编辑/当日日程窗口），
    /// 切页时内容起始位置会横向跳动。
    ///
    /// 注意本契约只管**区块级**留白（四边都 >= 12 的那种）。诸如 `8,0,0,0`
    /// （图标→文字）、`4,0,0,0` 属于**行内**间距，不在 UI_DESIGN §2.2 的
    /// 4/8/12/16/24 基线管辖范围内，强行归并会把密集行撑肿。
    /// </summary>
    [Fact]
    public void PageLevelPaddingIsUniformAcrossViews()
    {
        string canonical = "24,20,24,24";
        string[] files =
        [
            "Views/AssistantView.xaml",
            "Views/CalendarView.xaml",
            "Views/ProjectView.xaml",
            "Views/RecordView.xaml",
            "Views/SettingsView.xaml",
            "Views/StatisticsView.xaml",
            "Views/TodayView.xaml",
            "Views/TodoView.xaml",
            "Views/ToolsView.xaml",
            "DayScheduleWindow.xaml",
            "EventEditWindow.xaml",
        ];

        var offenders = new List<string>();
        foreach (string file in files)
        {
            string[] segments = ["src", "CcCalendar.Desktop", .. file.Split('/')];
            string content = ReadWorkspaceFile(segments);

            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(content, "Margin=\"(\\d+,\\d+,\\d+,\\d+)\""))
            {
                string value = match.Groups[1].Value;
                string[] parts = value.Split(',');
                bool blockLevel = int.Parse(parts[0], CultureInfo.InvariantCulture) >= 12
                    && int.Parse(parts[2], CultureInfo.InvariantCulture) >= 12;
                if (blockLevel && value != canonical)
                {
                    offenders.Add($"{file}: Margin=\"{value}\"（应为 {canonical}）");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "以下页面级留白与统一值不一致：\n" + string.Join("\n", offenders));
    }
    /// <summary>
    /// P67：界面里不得写死版本号。
    ///
    /// 修复前「设置 → 应用更新」那行写死 `当前版本 0.6.5；…`，且「查看更新内容」
    /// 展示的是一段写死的 0.6.5 说明——发版时无人更新，更新到 0.6.7 后界面仍显示 0.6.5。
    /// 版本号现在只由 csproj 的 &lt;Version&gt; 决定（`App.CurrentVersion`），
    /// 更新说明取自公网清单的 releaseNotes。
    /// </summary>
    [Fact]
    public void ViewsDoNotHardcodeAnApplicationVersion()
    {
        // 与 x.y.z 无关的"版本"用法（数据库 schema、Windows 注册表路径）在此放行，
        // 它们不是应用版本号。
        string[] allowed =
        [
            "CurrentVersion",        // Windows 注册表路径片段
            "DatabaseFormat",        // 数据库 schema 版本
            "SchemaVersion",
            "schemaVersion",
            "apiVersion",
            "minVersion",
        ];

        string[] sources =
        [
            "Views/SettingsView.xaml",
            "Views/SettingsView.xaml.cs",
            "MainWindow.xaml",
            "MainWindow.xaml.cs",
        ];

        var offenders = new List<string>();
        foreach (string file in sources)
        {
            string[] segments = ["src", "CcCalendar.Desktop", .. file.Split('/')];
            string content = ReadWorkspaceFile(segments);
            string[] lines = content.Split('\n');

            for (int index = 0; index < lines.Length; index++)
            {
                // 先去掉注释：说明"曾经写死成 0.6.5"的注释本身含版本号，不应算违规。
                string code = lines[index];
                int commentAt = code.IndexOf("//", StringComparison.Ordinal);
                if (commentAt >= 0)
                {
                    code = code[..commentAt];
                }

                int xmlDocAt = code.IndexOf("///", StringComparison.Ordinal);
                if (xmlDocAt >= 0)
                {
                    code = code[..xmlDocAt];
                }

                System.Text.RegularExpressions.Match match =
                    System.Text.RegularExpressions.Regex.Match(code, @"\b\d+\.\d+\.\d+\b");
                if (!match.Success)
                {
                    continue;
                }

                if (allowed.Any(token => lines[index].Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                offenders.Add($"{file}:{index + 1} 出现字面版本号 '{match.Value}'：{lines[index].Trim()}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "界面代码里出现写死的版本号，应改用 App.CurrentVersion 或清单的 releaseNotes：\n"
                + string.Join("\n", offenders));
    }
    private static string ReadWorkspaceFile(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CcCalendar.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. segments]));
    }
}
