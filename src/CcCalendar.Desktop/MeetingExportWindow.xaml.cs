using System.Windows;
using System.Windows.Controls;
using CcCalendar.Core.QuickAdd;
using CcCalendar.Core.Schedules;

namespace CcCalendar.Desktop;

public partial class MeetingExportWindow : Window
{
    private readonly CalendarEvent[] meetings;

    public MeetingExportWindow(DateOnly date, IReadOnlyList<CalendarEvent> events)
    {
        InitializeComponent();
        Title = $"导出 {date:yyyy年M月d日} 会议";
        meetings = events
            .Where(item => !string.IsNullOrWhiteSpace(item.MeetingInvitationText))
            .OrderBy(item => item.StartAtUtc)
            .ToArray();
        BuildItems();
    }

    private void BuildItems()
    {
        if (meetings.Length == 0)
        {
            MeetingItems.ItemsSource = new object[] { new TextBlock { Text = "当天没有已导入的会议邀请。", Margin = new Thickness(0, 8, 0, 8) } };
            return;
        }

        MeetingItems.ItemsSource = meetings.Select((meeting, index) => CreateMeetingPanel(meeting, index)).ToArray();
    }

    private Border CreateMeetingPanel(CalendarEvent meeting, int index)
    {
        var panel = new Border
        {
            BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
        };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var title = new TextBlock { FontWeight = FontWeights.SemiBold, Text = $"{index + 1}. {meeting.Title}" };
        Grid.SetRow(title, 0);
        layout.Children.Add(title);
        DateTimeOffset? start = meeting.StartAtUtc;
        DateTimeOffset? end = meeting.EndAtUtc;
        var time = new TextBlock { Margin = new Thickness(0, 4, 0, 8), Text = $"{meeting.Location ?? "未指定会议室"}  {start:yyyy-MM-dd HH:mm}–{end:HH:mm}" };
        Grid.SetRow(time, 1);
        layout.Children.Add(time);
        var invitation = new TextBox { Text = meeting.MeetingInvitationText, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 72, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(invitation, 2);
        layout.Children.Add(invitation);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var copyAll = new Button { Content = "复制全部信息", Tag = meeting, Margin = new Thickness(0, 0, 8, 0) };
        copyAll.Click += CopyMeetingAllClick;
        buttons.Children.Add(copyAll);
        var copyLink = new Button { Content = "复制会议号和链接", Tag = meeting };
        copyLink.Click += CopyMeetingLinkClick;
        buttons.Children.Add(copyLink);
        Grid.SetRow(buttons, 3);
        layout.Children.Add(buttons);
        panel.Child = layout;
        return panel;
    }

    private void CopyAllClick(object sender, RoutedEventArgs e)
    {
        CopyText(string.Join(Environment.NewLine + Environment.NewLine, meetings.Select(FullText)));
    }

    private void CopyMeetingAllClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CalendarEvent meeting })
        {
            CopyText(FullText(meeting));
        }
    }

    private void CopyMeetingLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CalendarEvent meeting })
        {
            CopyText(NumberAndLink(meeting));
        }
    }

    internal static string FullText(CalendarEvent meeting) => meeting.MeetingInvitationText ?? string.Empty;

    internal static string NumberAndLink(CalendarEvent meeting)
    {
        string text = meeting.MeetingInvitationText ?? string.Empty;
        if (!TencentMeetingInvitationParser.TryParse(text, out TencentMeetingInvitation? invitation) || invitation is null)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, new[]
        {
            invitation.MeetingNumber is { Length: > 0 } number ? $"会议号：{number}" : null,
            invitation.JoinUrl,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void CopyText(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
        }
    }
}
