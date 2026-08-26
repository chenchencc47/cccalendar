using System.Windows;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop;

public partial class MeetingDetailsWindow : Window
{
    private readonly RoomOccupiedBlock block;
    private readonly Func<RoomOccupiedBlock, Task>? addToCalendar;
    private readonly Func<RoomOccupiedBlock, Task>? deleteBooking;

    public MeetingDetailsWindow(
        RoomOccupiedBlock block,
        Func<RoomOccupiedBlock, Task>? addToCalendar = null,
        Func<RoomOccupiedBlock, Task>? deleteBooking = null)
    {
        this.block = block;
        this.addToCalendar = addToCalendar;
        this.deleteBooking = deleteBooking;
        InitializeComponent();
        TitleText.Text = block.Title;
        string organizer = string.IsNullOrWhiteSpace(block.OrganizerName)
            ? "未知成员"
            : block.OrganizerName;
        MetaText.Text = $"{block.Room}  {block.Start:HH\\:mm}–{block.End:HH\\:mm}  会议人员：{organizer}";
        InvitationText.Text = block.MeetingInvitationText ?? "没有保存原始会议邀请文本。";
        AddToCalendarButton.Visibility = addToCalendar is not null
            && block.IsRemoteBooking
            && !block.IsOwnedByCurrentUser
            ? Visibility.Visible
            : Visibility.Collapsed;
        DeleteBookingButton.Visibility = deleteBooking is not null
            && block.IsRemoteBooking
            && block.IsOwnedByCurrentUser
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void AddToCalendarClick(object sender, RoutedEventArgs e)
    {
        if (addToCalendar is null)
        {
            return;
        }

        AddToCalendarButton.IsEnabled = false;
        try
        {
            await addToCalendar(block);
            MessageBox.Show(this, "已添加到我的日程。", "cccalendar", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception exception)
        {
            AddToCalendarButton.IsEnabled = true;
            MessageBox.Show(this, $"添加失败：{exception.Message}", "cccalendar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void DeleteBookingClick(object sender, RoutedEventArgs e)
    {
        if (deleteBooking is null)
        {
            return;
        }

        if (MessageBox.Show(this, "确定删除自己创建的云端预约吗？删除后所有团队成员都会同步移除。", "删除预约", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteBookingButton.IsEnabled = false;
        try
        {
            await deleteBooking(block);
            Close();
        }
        catch (Exception exception)
        {
            DeleteBookingButton.IsEnabled = true;
            MessageBox.Show(this, $"删除失败：{exception.Message}", "cccalendar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyAllClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(block.MeetingInvitationText))
        {
            Clipboard.SetText(block.MeetingInvitationText);
        }
    }

    private void CopyLinkClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(block.MeetingInvitationText))
        {
            var meeting = CcCalendar.Core.Schedules.CalendarEvent.CreateTimed(
                block.Title,
                null,
                DateTimeOffset.Now,
                DateTimeOffset.Now.AddMinutes(1),
                TimeZoneInfo.Local.Id,
                block.Room,
                block.MeetingInvitationText);
            string text = MeetingExportWindow.NumberAndLink(meeting);
            if (!string.IsNullOrWhiteSpace(text))
            {
                Clipboard.SetText(text);
            }
        }
    }
}
