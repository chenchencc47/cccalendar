using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Desktop;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// 真实实例化快速新增窗口（STA），验证双击日历某天打开时
/// 日期/时间字段可见、可绑定、模板部件工作。用于防止
/// "日程类型下不显示日期、无法选择"的回归。
/// </summary>
public sealed class QuickAddWindowRuntimeTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Fact]
    public void EventModeShowsBoundDateAndTimeControlsWhenOpenedForDate()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.FromHours(8))),
                eventDate: DateOnly.Parse("2026-08-19", Invariant));
            try
            {
                window.Show();
                DoEvents(window);

                // 类型默认日程且日程字段可见。
                Assert.Equal(QuickAddKind.Event, window.SelectedOption.Kind);
                Assert.Equal(Visibility.Visible, window.EventFields.Visibility);

                // 日期已绑定且模板文本框显示日期文本。
                Assert.Equal(DateTime.Parse("2026-08-19", Invariant), window.EventDatePicker.SelectedDate);
                var dateTextBox = (TextBox)window.EventDatePicker.Template.FindName(
                    "PART_TextBox", window.EventDatePicker);
                Assert.NotNull(dateTextBox);
                Assert.NotEmpty(dateTextBox.Text);

                // 开始/结束时间已绑定且可编辑文本框显示时间文本。
                var startTimeTextBox = (TextBox)window.StartTimeBox.Template.FindName(
                    "PART_EditableTextBox", window.StartTimeBox);
                Assert.NotNull(startTimeTextBox);
                Assert.Equal("09:00", startTimeTextBox.Text);
                var endTimeTextBox = (TextBox)window.EndTimeBox.Template.FindName(
                    "PART_EditableTextBox", window.EndTimeBox);
                Assert.NotNull(endTimeTextBox);
                Assert.Equal("10:00", endTimeTextBox.Text);

                // 类型下拉可以打开（模板含 ToggleButton 且命中测试可用）。
                window.KindBox.IsDropDownOpen = true;
                DoEvents(window);
                Assert.True(window.KindBox.IsDropDownOpen);

                // 日期弹层可打开、日历部件存在（用户点击日期后框架写回 DatePicker.SelectedDate）。
                var popup = (System.Windows.Controls.Primitives.Popup)window.EventDatePicker.Template.FindName(
                    "PART_Popup", window.EventDatePicker);
                Assert.NotNull(popup);
                var calendar = (System.Windows.Controls.Calendar)window.EventDatePicker.Template.FindName(
                    "PART_Calendar", window.EventDatePicker);
                Assert.NotNull(calendar);
                window.EventDatePicker.IsDropDownOpen = true;
                DoEvents(window);
                Assert.True(window.EventDatePicker.IsDropDownOpen);
                window.EventDatePicker.IsDropDownOpen = false;

                // 选择新日期后回写到窗体属性（双向绑定链路）。
                window.EventDatePicker.SelectedDate = DateTime.Parse("2026-08-21", Invariant);
                DoEvents(window);
                Assert.Equal(DateTime.Parse("2026-08-21", Invariant), window.SelectedDate);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TeamRoomsReplaceRoomDropdownOptions()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(8))),
                teamBoardLoader: (_, _) => Task.FromResult<TeamRoomBoardSnapshot?>(
                    new TeamRoomBoardSnapshot(["云会议室A", "云会议室B"], [])));
            try
            {
                window.Show();
                DoEvents(window);

                Assert.Equal([string.Empty, "云会议室A", "云会议室B"], window.RoomOptions);
                Assert.Equal(string.Empty, window.SelectedRoom);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SelectingStartTimeMovesEndTimeToOneHourLater()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(8))));
            try
            {
                window.Show();
                DoEvents(window);

                Assert.Equal("10:00", window.SelectedStartTime.Label);
                Assert.Equal("11:00", window.SelectedEndTime.Label);

                window.SelectedStartTime = window.TimeOptions
                    .Single(option => option.Value == new TimeOnly(14, 0));
                DoEvents(window);

                Assert.Equal("14:00", window.SelectedStartTime.Label);
                Assert.Equal("15:00", window.SelectedEndTime.Label);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MeetingReminderIsOptionalAndIncludedWhenEnabled()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(8))));
            try
            {
                window.Show();
                DoEvents(window);
                window.TitleBox.Text = "项目会议";
                Assert.False(window.MeetingReminderCheckBox.IsChecked);
                Assert.True(window.TryBuildRequests(out _));
                Assert.Null(Assert.Single(window.Requests).ReminderLeadMinutes);

                window.MeetingReminderCheckBox.IsChecked = true;
                window.MeetingReminderMinutesBox.Text = "30";
                DoEvents(window);
                Assert.True(window.TryBuildRequests(out _));
                Assert.Equal(30, Assert.Single(window.Requests).ReminderLeadMinutes);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TypingInvitationTextIntoBoxDoesNotFillUntilImport()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(8))));
            try
            {
                window.Show();
                DoEvents(window);

                // 手动把邀请内容粘进输入框：只记录文本，不自动改动下方字段。
                window.MeetingInvitationBox.Text = """
                    会议主题：数字化平台重构项目每周例会
                    会议时间：2026/08/27 14:10-15:10 (GMT+08:00) 中国标准时间 - 北京
                    #腾讯会议：749-5361-1348
                    与会地点：佛山西樵研发中心三楼会议室
                    """;
                DoEvents(window);

                Assert.Equal(string.Empty, window.TitleBox.Text);
                Assert.Equal(string.Empty, window.MeetingNumberBox.Text);

                Assert.True(window.ApplyMeetingInvitation(window.MeetingInvitationBox.Text));
                Assert.Equal("数字化平台重构项目每周例会", window.TitleBox.Text);
                Assert.Equal("749-5361-1348", window.MeetingNumberBox.Text);
                Assert.Equal(DateTime.Parse("2026-08-27", Invariant), window.SelectedDate);
                Assert.Equal("14:10", window.SelectedStartTime.Label);
                Assert.Equal("15:10", window.SelectedEndTime.Label);
                Assert.Equal("研发中心三楼会议室", window.SelectedRoom);

                // 非邀请文本不应改动已填充字段。
                window.MeetingInvitationBox.Text = "普通备注文字";
                DoEvents(window);
                Assert.Equal("数字化平台重构项目每周例会", window.TitleBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplyMeetingInvitationFillsFormFieldsAndMatchesRoom()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(8))));
            try
            {
                window.Show();
                DoEvents(window);

                bool applied = window.ApplyMeetingInvitation("""
                    周家丞 邀请您参加腾讯会议
                    会议主题：数字化平台重构项目每周例会
                    会议时间：2026/08/27 14:10-15:10 (GMT+08:00) 中国标准时间 - 北京
                    点击链接入会，或添加至会议列表：
                    https://meeting.tencent.com/dm/AzRnftL11yo6
                    #腾讯会议：749-5361-1348
                    与会地点：佛山西樵研发中心三楼会议室
                    """);
                DoEvents(window);

                Assert.True(applied);
                Assert.Equal("数字化平台重构项目每周例会", window.TitleBox.Text);
                Assert.Equal("749-5361-1348", window.MeetingNumberBox.Text);
                Assert.Equal(DateTime.Parse("2026-08-27", Invariant), window.SelectedDate);
                Assert.Equal("14:10", window.SelectedStartTime.Label);
                Assert.Equal("15:10", window.SelectedEndTime.Label);
                Assert.Equal("研发中心三楼会议室", window.SelectedRoom);
                Assert.True(window.TryBuildRequests(out _));
                string invitation = Assert.IsType<string>(Assert.Single(window.Requests).MeetingInvitationText);
                Assert.Contains("会议主题：数字化平台重构项目每周例会", invitation);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplyMeetingInvitationAcceptsChineseTencentFormat()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(8))));
            try
            {
                window.Show();
                DoEvents(window);

                bool applied = window.ApplyMeetingInvitation("""
                    会议名称：销售排产订单管理流程蓝图沟通会议
                    会议时间：2026年8月24日（星期一）14:30-17:00
                    会议地点：集团聚英堂会议室
                    点击链接入会，或添加至会议列表：https://meeting.tencent.com/dm/fYF62CD5t2LO#腾讯会议：242-471-769复制该信息，打开手机腾讯会议即可参与
                    """);

                Assert.True(applied);
                Assert.Equal("销售排产订单管理流程蓝图沟通会议", window.TitleBox.Text);
                Assert.Equal("242-471-769", window.MeetingNumberBox.Text);
                Assert.Equal(DateTime.Parse("2026-08-24", Invariant), window.SelectedDate);
                Assert.Equal("14:30", window.SelectedStartTime.Label);
                Assert.Equal("17:00", window.SelectedEndTime.Label);
                Assert.Equal("聚英堂会议室", window.SelectedRoom);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplyMeetingInvitationReturnsFalseForNonInvitationText()
    {
        RunOnSta(() =>
        {
            var window = new QuickAddWindow(
                new FrozenTimeProvider(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(8))));
            try
            {
                window.Show();
                DoEvents(window);

                bool applied = window.ApplyMeetingInvitation("今天下午开会聊聊");

                Assert.False(applied);
                Assert.Equal(string.Empty, window.TitleBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void DoEvents(Window window)
    {
        window.Dispatcher.Invoke(DispatcherPriority.Background, () => { });
        window.Dispatcher.Invoke(DispatcherPriority.Render, () => { });
    }

    private static void RunOnSta(Action action) => WpfRuntimeHost.Run(action);

    private sealed class FrozenTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone(
            "Frozen", localNow.Offset, null, null);
    }
}
