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
        Assert.Contains("Material Symbols download asset", mainWindow, StringComparison.Ordinal);
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
    public void AssistantMessagesExposeCollapsibleThinkingBlock()
    {
        string assistant = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        Assert.Contains("CornerRadius=\"18\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Header=\"思考\"", assistant, StringComparison.Ordinal);
        Assert.Contains("{Binding ThinkingText}", assistant, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly=\"True\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text, Mode=OneWay}\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Click=\"CopyMessageClick\"", assistant, StringComparison.Ordinal);
        Assert.Contains("Value=\"AI\"", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("ThinkEnabled", assistant, StringComparison.Ordinal);
        Assert.DoesNotContain("DeepSearchEnabled", assistant, StringComparison.Ordinal);
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
        Assert.Contains("x:Name=\"AssistantContextBar\"", assistantView, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantActionsExposeAccessibleNames()
    {
        string assistantView = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "AssistantView.xaml");

        Assert.Contains("AutomationProperties.Name=\"复制消息\"", assistantView, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"停止请求\"", assistantView, StringComparison.Ordinal);
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
