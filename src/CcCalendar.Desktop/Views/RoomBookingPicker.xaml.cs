using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CcCalendar.Core.Schedules;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public sealed record RoomBookingPicked(
    DateOnly Date,
    string Room,
    TimeOnly Start,
    TimeOnly End,
    bool IsFree);

/// <summary>
/// 会议室时间视图区：顶部「前后箭头 + 日期」栏、左侧 24 小时刻度、
/// 会议室列头和可拖选的半小时预订板。
/// </summary>
public partial class RoomBookingPicker : UserControl
{
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    private Func<DateOnly, IReadOnlyList<CalendarEvent>> loadEvents = _ => [];
    private Func<DateOnly, TimeZoneInfo, Task<TeamRoomBoardSnapshot?>>? teamBoardLoader;
    private TeamRoomBoardSnapshot? teamSnapshot;
    private int teamLoadGeneration;
    private DispatcherTimer? teamRefreshTimer;
    private bool isRefreshingTeamBoard;
    private TimeZoneInfo timeZone = TimeZoneInfo.Local;
    private Guid? excludeEventId;

    /// <summary>用户在板上提交了选择（点选/拖选/边缘调整）。</summary>
    public event EventHandler<RoomBookingPicked>? Picked;

    /// <summary>日期被前后箭头切换。</summary>
    public event EventHandler<DateOnly>? DateSwitched;

    /// <summary>房间列集合变化（团队目录加载完成替换本地目录）。</summary>
    public event EventHandler<IReadOnlyList<string>>? RoomsChanged;

    public event EventHandler<RoomOccupiedBlock>? BookingDoubleClicked;

    public RoomBookingPicker()
    {
        InitializeComponent();
        // 8 点上班：只展示 8:00–24:00 的刻度与网格。
        HourLabels.ItemsSource = Enumerable.Range(8, 16)
            .Select(hour => $"{hour:00}:00")
            .ToArray();
        CurrentRooms = MeetingRoomCatalog.Rooms;
        RoomHeader.ItemsSource = CurrentRooms;
        Board.Width = CurrentRooms.Count * RoomBookingBoardControl.RoomWidth;
        Board.Height = RoomBookingBoardControl.VisibleCells * RoomBookingBoardControl.CellHeight;
        Board.BookingDoubleClicked += (_, block) => BookingDoubleClicked?.Invoke(this, block);
        Unloaded += (_, _) => teamRefreshTimer?.Stop();
    }

    public DateOnly Date { get; private set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>当前看板房间列（团队模式下来自服务端目录）。</summary>
    public IReadOnlyList<string> CurrentRooms { get; private set; }

    public Task RefreshTeamBoardAsync()
    {
        return teamBoardLoader is null
            ? Task.CompletedTask
            : LoadTeamBoardAsync(++teamLoadGeneration);
    }

    public void Initialize(
        DateOnly initialDate,
        Func<DateOnly, IReadOnlyList<CalendarEvent>> eventsLoader,
        TimeZoneInfo zone,
        Guid? excludedEventId = null,
        Func<DateOnly, TimeZoneInfo, Task<TeamRoomBoardSnapshot?>>? teamBoardLoader = null)
    {
        ArgumentNullException.ThrowIfNull(eventsLoader);
        ArgumentNullException.ThrowIfNull(zone);
        loadEvents = eventsLoader;
        timeZone = zone;
        excludeEventId = excludedEventId;
        this.teamBoardLoader = teamBoardLoader;
        teamSnapshot = null;
        Date = initialDate;
        ConfigureTeamRefreshTimer();
        Rebuild();
    }

    private void ConfigureTeamRefreshTimer()
    {
        if (teamBoardLoader is null)
        {
            teamRefreshTimer?.Stop();
            return;
        }

        teamRefreshTimer ??= new DispatcherTimer(
            TimeSpan.FromSeconds(15),
            DispatcherPriority.Background,
            async (_, _) =>
            {
                if (isRefreshingTeamBoard)
                {
                    return;
                }

                isRefreshingTeamBoard = true;
                try
                {
                    await RefreshTeamBoardAsync();
                }
                finally
                {
                    isRefreshingTeamBoard = false;
                }
            },
            Dispatcher);
        teamRefreshTimer.Start();
    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        var window = new MeetingExportWindow(Date, loadEvents(Date));
        Window? owner = Window.GetWindow(this);
        if (owner is not null)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }

    private void PreviousClick(object sender, RoutedEventArgs e)
    {
        Date = Date.AddDays(-1);
        Rebuild();
    }

    private void NextClick(object sender, RoutedEventArgs e)
    {
        Date = Date.AddDays(1);
        Rebuild();
    }

    private void BoardSelectionChanged(object? sender, EventArgs e)
    {
        if (Board.Board?.TryGetPrimarySelection(
                out string room,
                out TimeOnly start,
                out TimeOnly end,
                out bool isFree) == true)
        {
            Picked?.Invoke(this, new RoomBookingPicked(Date, room, start, end, isFree));
        }
    }

    private void BodyScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 看板横向滚动时同步会议室表头，保持列对齐。
        if (Math.Abs(HeaderScroll.HorizontalOffset - e.HorizontalOffset) > 0.01)
        {
            HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }

    private void Rebuild()
    {
        RenderBoard();
        RefreshDateBar();
        DateSwitched?.Invoke(this, Date);
        if (teamBoardLoader is not null)
        {
            _ = LoadTeamBoardAsync(++teamLoadGeneration);
        }
    }

    private async Task LoadTeamBoardAsync(int generation)
    {
        TeamRoomBoardSnapshot? snapshot;
        try
        {
            snapshot = await teamBoardLoader!(Date, timeZone);
        }
        catch (Exception exception)
        {
            // Keep the last successful cloud snapshot. Falling back to an old local
            // catalog makes an authentication/network failure look like cloud data loss.
            System.Diagnostics.Debug.WriteLine($"Team room board refresh failed: {exception.Message}");
            return;
        }

        if (snapshot is null || generation != teamLoadGeneration)
        {
            return;
        }

        teamSnapshot = snapshot;
        RenderBoard();
    }

    private void RenderBoard()
    {
        IReadOnlyList<string> catalogRooms = teamSnapshot is { Rooms.Count: > 0 }
            ? teamSnapshot.Rooms
            : MeetingRoomCatalog.Rooms;

        IReadOnlyList<CalendarEvent> events = loadEvents(Date) ?? [];
        var occurrences = new List<RoomBookingOccurrence>();
        var blocks = new List<RoomOccupiedBlock>();
        foreach (CalendarEvent item in events)
        {
            if (item is { IsAllDay: true } || item.StartAtUtc is null || item.EndAtUtc is null)
            {
                continue;
            }

            if (item.Id == excludeEventId)
            {
                continue;
            }

            string? room = item.Location;
            if (string.IsNullOrWhiteSpace(room) || !catalogRooms.Contains(room))
            {
                continue;
            }

            (int startCell, int endCellExclusive) = RoomBookingBoard.GetClippedCells(
                Date, item.StartAtUtc.Value, item.EndAtUtc.Value, timeZone);
            if (endCellExclusive <= startCell)
            {
                continue;
            }

            // A successful cloud sync mirrors the local event. Render the cloud
            // block once so the organizer and invitation are consistent on every PC.
            if (teamSnapshot?.Bookings.Any(booking =>
                    string.Equals(booking.Room, room, StringComparison.Ordinal)
                    && string.Equals(booking.Title, item.Title, StringComparison.Ordinal)
                    && booking.StartUtc == item.StartAtUtc.Value
                    && booking.EndUtc == item.EndAtUtc.Value) == true)
            {
                continue;
            }

            occurrences.Add(new RoomBookingOccurrence(room, item.StartAtUtc.Value, item.EndAtUtc.Value));
            blocks.Add(new RoomOccupiedBlock(
                room,
                startCell,
                endCellExclusive,
                item.Title,
                TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.StartAtUtc.Value, timeZone).DateTime),
                TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.EndAtUtc.Value, timeZone).DateTime),
                item.Id,
                item.MeetingInvitationText,
                ExtractMeetingNumber(item.Title),
                ExtractJoinUrl(item.MeetingInvitationText),
                null,
                OrganizerId: null,
                IsOwnedByCurrentUser: true,
                IsRemoteBooking: false,
                Date: Date));
        }

        if (teamSnapshot is not null)
        {
            foreach (TeamRoomBooking booking in teamSnapshot.Bookings)
            {
                if (!catalogRooms.Contains(booking.Room))
                {
                    continue;
                }

                (int startCell, int endCellExclusive) = RoomBookingBoard.GetClippedCells(
                    Date, booking.StartUtc, booking.EndUtc, timeZone);
                if (endCellExclusive <= startCell)
                {
                    continue;
                }

                occurrences.Add(new RoomBookingOccurrence(booking.Room, booking.StartUtc, booking.EndUtc));
                blocks.Add(new RoomOccupiedBlock(
                    booking.Room,
                    startCell,
                    endCellExclusive,
                    booking.Title,
                    TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(booking.StartUtc, timeZone).DateTime),
                    TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(booking.EndUtc, timeZone).DateTime),
                    EventId: booking.Id,
                    MeetingInvitationText: booking.MeetingInvitationText,
                    MeetingNumber: ExtractMeetingNumber(booking.MeetingInvitationText ?? string.Empty),
                    JoinUrl: ExtractJoinUrl(booking.MeetingInvitationText),
                    OrganizerName: booking.OrganizerName,
                    OrganizerId: booking.OrganizerId,
                    IsOwnedByCurrentUser: booking.IsOwnedByCurrentUser,
                    IsRemoteBooking: true,
                    Date: Date));
            }
        }

        IReadOnlyList<string> rooms = RoomBookingOrdering.MoveBookedRoomsToFront(
            catalogRooms,
            occurrences.Select(occurrence => occurrence.Room));
        ApplyRooms(rooms);

        Board.SetContent(
            new RoomBookingBoard(Date, rooms, occurrences, timeZone),
            blocks,
            rooms);
    }

    private void ApplyRooms(IReadOnlyList<string> rooms)
    {
        if (rooms.Count == CurrentRooms.Count && rooms.SequenceEqual(CurrentRooms))
        {
            return;
        }

        CurrentRooms = rooms;
        RoomHeader.ItemsSource = rooms;
        Board.Width = rooms.Count * RoomBookingBoardControl.RoomWidth;
        RoomsChanged?.Invoke(this, rooms);
    }

    private void RefreshDateBar()
    {
        bool isToday = Date == DateOnly.FromDateTime(DateTime.Today);
        string weekDay = ChineseCulture.DateTimeFormat.GetAbbreviatedDayName(Date.DayOfWeek);
        DateText.Text = $"{Date.Month}月{Date.Day}日 {weekDay}{(isToday ? " (今天)" : string.Empty)}";
        DateText.Foreground = isToday
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextSecondaryBrush");
        DateText.FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private static string? ExtractMeetingNumber(string title)
    {
        const string marker = "腾讯会议 ";
        int start = title.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        int end = title.IndexOf('）', start);
        return (end < 0 ? title[start..] : title[start..end]).Trim();
    }

    private static string? ExtractJoinUrl(string? invitation)
    {
        if (string.IsNullOrWhiteSpace(invitation))
        {
            return null;
        }

        int start = invitation.IndexOf("https://meeting.tencent.com/", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        int end = invitation.IndexOfAny([' ', '\r', '\n', '\t'], start);
        return (end < 0 ? invitation[start..] : invitation[start..end]).Trim();
    }
}
