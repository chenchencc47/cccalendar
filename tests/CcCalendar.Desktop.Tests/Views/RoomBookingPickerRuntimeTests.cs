using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CcCalendar.Core.Schedules;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// 真实实例化会议室时间视图（STA + 布局 + 渲染），验证：
/// 1) 预订板位于小时刻度列右侧、会议室表头下方（布局无偏移）；
/// 2) 网格线从 8:00 顶格开始逐半小时绘制（渲染无缺行）；
/// 3) 点击 8:00-8:30 单元格的命中测试解析到预订板自身（可选中）。
/// 对应缺陷：看板 8:00-10:00 区域不可选中且上半区无网格线。
/// </summary>
public sealed class RoomBookingPickerRuntimeTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Fact]
    public void BoardIsLaidOutBelowHeaderAndHitTestsToItself()
    {
        RunOnSta(() =>
        {
            var picker = new RoomBookingPicker();
            picker.Initialize(
                DateOnly.Parse("2026-08-20", Invariant),
                _ => Array.Empty<CalendarEvent>(),
                TimeZoneInfo.Local);
            var window = new Window { Content = picker, Width = 722, Height = 627 };
            try
            {
                window.Show();
                DoEvents(window);

                RoomBookingBoardControl board = picker.Board;
                Assert.True(board.ActualWidth > 0 && board.ActualHeight > 0,
                    $"板应完成布局，实际 {board.ActualWidth}x{board.ActualHeight}");

                // 板相对视图的位置：x 应紧跟 44px 小时刻度列，y 应位于
                // 顶部按钮行(32) + 8 间隙 + 表头行(26) = 66 之下。
                Point topLeft = board.TransformToAncestor(picker).Transform(new Point(0, 0));
                Assert.True(Math.Abs(topLeft.X - 44) < 1.5,
                    $"板左边缘应紧贴刻度列右侧(44)，实际 {topLeft.X}");
                Assert.True(Math.Abs(topLeft.Y - 66) < 1.5,
                    $"板顶边应位于表头下方(66)，实际 {topLeft.Y}");

                // 命中测试 8:00-8:30 单元格中心（第一会议室列）。
                Point cellCenter = board.TransformToAncestor(picker).Transform(new Point(
                    RoomBookingBoardControl.RoomWidth / 2,
                    RoomBookingBoardControl.CellHeight / 2));
                HitTestResult? hit = VisualTreeHelper.HitTest(picker, cellCenter);
                Assert.NotNull(hit);
                bool hitsBoard = IsSelfOrDescendant(hit.VisualHit, board);
                Assert.True(hitsBoard,
                    $"点击 8:00-8:30 应命中预订板，实际命中 {hit.VisualHit.GetType().Name}");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void BoardRendersHourGridLinesStartingAtTopEdge()
    {
        RunOnSta(() =>
        {
            var picker = new RoomBookingPicker();
            picker.Initialize(
                DateOnly.Parse("2026-08-20", Invariant),
                _ => Array.Empty<CalendarEvent>(),
                TimeZoneInfo.Local);
            picker.Measure(new Size(722, 627));
            picker.Arrange(new Rect(0, 0, 722, 627));
            picker.UpdateLayout();

            RoomBookingBoardControl board = picker.Board;
            Point topLeft = board.TransformToAncestor(picker).Transform(new Point(0, 0));

            var bitmap = new RenderTargetBitmap(722, 627, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(picker);

            // 逐小时检查水平线：板顶(8:00) + 26k 处应有非白像素（整点线）。
            var missing = new List<double>();
            for (int hour = 0; hour <= 8; hour++)
            {
                double y = topLeft.Y + hour * 2 * RoomBookingBoardControl.CellHeight;
                int py = (int)Math.Round(y);
                bool lineFound = false;
                for (int x = (int)topLeft.X + 10; x < (int)(topLeft.X + board.ActualWidth) - 10; x += 5)
                {
                    Color c = GetPixel(bitmap, x, py);
                    if (c.R < 245 || c.G < 245 || c.B < 245)
                    {
                        lineFound = true;
                        break;
                    }
                }

                if (!lineFound)
                {
                    missing.Add(y);
                }
            }

            Assert.Empty(missing);
        });
    }

    [Fact]
    public void TeamBoardLoaderAppliesServerRoomsAndRemoteOccupancy()
    {
        RunOnSta(() =>
        {
            var requestedDates = new List<DateOnly>();
            var picker = new RoomBookingPicker();
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            picker.Initialize(
                DateOnly.Parse("2026-08-24", Invariant),
                _ => Array.Empty<CalendarEvent>(),
                zone,
                teamBoardLoader: (date, _) =>
                {
                    requestedDates.Add(date);
                    // 01:00Z–02:00Z = 09:00–10:00 +08:00。
                    return Task.FromResult<TeamRoomBoardSnapshot?>(new TeamRoomBoardSnapshot(
                        ["云会议室A", "云会议室B"],
                        [new TeamRoomBooking(
                            "云会议室A",
                            "远程周会",
                        new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
                        "周家丞") ]));
                });

            Assert.Equal([DateOnly.Parse("2026-08-24", Invariant)], requestedDates);
            Assert.Equal(["云会议室A", "云会议室B"], picker.CurrentRooms);
            RoomBookingBoard? board = picker.Board.Board;
            Assert.NotNull(board);
            Assert.Equal(["云会议室A", "云会议室B"], board!.Rooms);
            Assert.True(board.IsOccupied("云会议室A", 18), "09:00 应被远程预约占用");
            Assert.False(board.IsOccupied("云会议室A", 20), "10:00 起应空闲（半开区间）");
            Assert.False(board.IsOccupied("云会议室B", 18));

            picker.NextButton.RaiseEvent(
                new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(
            [
                DateOnly.Parse("2026-08-24", Invariant),
                DateOnly.Parse("2026-08-25", Invariant),
            ],
                requestedDates);
        });
    }

    [Fact]
    public void RoomsWithMeetingsMoveAheadOfEmptyRoomsInCatalogOrder()
    {
        RunOnSta(() =>
        {
            var picker = new RoomBookingPicker();
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            picker.Initialize(
                DateOnly.Parse("2026-08-24", Invariant),
                _ =>
                [
                    CalendarEvent.CreateTimed(
                        "研发会议",
                        null,
                        new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 8, 24, 3, 0, 0, TimeSpan.Zero),
                        "China Standard Time",
                        "研发中心三楼会议室"),
                    CalendarEvent.CreateTimed(
                        "技术会议",
                        null,
                        new DateTimeOffset(2026, 8, 24, 4, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 8, 24, 5, 0, 0, TimeSpan.Zero),
                        "China Standard Time",
                        "技术中心二楼会议室"),
                ],
                zone);

            Assert.Equal(
                [
                    "技术中心二楼会议室",
                    "研发中心三楼会议室",
                    .. MeetingRoomCatalog.Rooms.Where(room =>
                        room is not "技术中心二楼会议室" and not "研发中心三楼会议室"),
                ],
                picker.CurrentRooms);
        });
    }

    [Fact]
    public void RemoteBookingDeletionMatchesItsLocalMirrorByRoomTitleAndMinuteTimes()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        CalendarEvent matching = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            new DateTimeOffset(2026, 8, 24, 10, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 11, 10, 0, TimeSpan.Zero),
            zone.Id,
            "项目组二楼会议室");
        CalendarEvent differentRoom = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            matching.StartAtUtc!.Value,
            matching.EndAtUtc!.Value,
            zone.Id,
            "生产组会议室");
        CalendarEvent differentTime = CalendarEvent.CreateTimed(
            "每日例会",
            null,
            new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 11, 0, 0, TimeSpan.Zero),
            zone.Id,
            "项目组二楼会议室");
        var block = new RoomOccupiedBlock(
            "项目组二楼会议室",
            20,
            22,
            "每日例会",
            new TimeOnly(18, 10),
            new TimeOnly(19, 10),
            Date: new DateOnly(2026, 8, 24),
            IsRemoteBooking: true,
            IsOwnedByCurrentUser: true);

        Guid[] matched = MainWindow.FindLocalEventIdsForRemoteBooking(
            [matching, differentRoom, differentTime],
            block,
            zone);

        Assert.Equal([matching.Id], matched);
    }

    [Fact]
    public void TeamBookingAlsoMovesBookedRoomAheadOfEmptyRooms()
    {
        RunOnSta(() =>
        {
            var picker = new RoomBookingPicker();
            picker.Initialize(
                DateOnly.Parse("2026-08-24", Invariant),
                _ => Array.Empty<CalendarEvent>(),
                TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"),
                teamBoardLoader: (_, _) => Task.FromResult<TeamRoomBoardSnapshot?>(
                    new TeamRoomBoardSnapshot(
                        ["云会议室A", "云会议室B"],
                        [new TeamRoomBooking(
                            "云会议室B",
                            "远程会议",
                            new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero),
                            new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
                            "周家丞")])));

            Assert.Equal(["云会议室B", "云会议室A"], picker.CurrentRooms);
            Assert.Equal(["云会议室B", "云会议室A"], picker.Board.Board!.Rooms);
            RoomOccupiedBlock block = Assert.Single(picker.Board.OccupiedBlocks);
            Assert.Equal("周家丞", block.OrganizerName);
        });
    }

    [Fact]
    public void TeamBoardLoaderFailureKeepsLocalRoomView()
    {
        RunOnSta(() =>
        {
            var picker = new RoomBookingPicker();
            picker.Initialize(
                DateOnly.Parse("2026-08-24", Invariant),
                _ => Array.Empty<CalendarEvent>(),
                TimeZoneInfo.Local,
                teamBoardLoader: (_, _) => Task.FromException<TeamRoomBoardSnapshot?>(
                    new HttpRequestException("offline")));

            Assert.Equal(MeetingRoomCatalog.Rooms, picker.CurrentRooms);
        });
    }

    [Fact]
    public void HoveringOccupiedBlockShowsBookingDetailsToolTip()
    {
        RunOnSta(() =>
        {
            var picker = new RoomBookingPicker();
            picker.Initialize(
                DateOnly.Parse("2026-08-26", Invariant),
                _ =>
                {
                    CalendarEvent meeting = CalendarEvent.CreateTimed(
                        "数字化平台重构项目每周例会（腾讯会议 749-5361-1348）",
                        null,
                        DateTimeOffset.Parse("2026-08-26 14:00:00 +08:00", Invariant),
                        DateTimeOffset.Parse("2026-08-26 15:00:00 +08:00", Invariant),
                        "China Standard Time",
                        "研发中心三楼会议室");
                    return [meeting];
                },
                TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
            var window = new Window { Content = picker, Width = 722, Height = 627 };
            try
            {
                window.Show();
                DoEvents(window);

                RoomBookingBoardControl board = picker.Board;
                // 悬停研发中心三楼会议室列（有预约的房间会被前移）14:30 格中心。
                double researchRoomCenterX = (Array.IndexOf(
                    [.. picker.CurrentRooms],
                    "研发中心三楼会议室") + 0.5) * RoomBookingBoardControl.RoomWidth;
                board.UpdateHoverToolTip(new Point(
                    researchRoomCenterX,
                    (28 - RoomBookingBoardControl.FirstVisibleCell + 0.5) * RoomBookingBoardControl.CellHeight));
                string? toolTip = board.ToolTip as string;
                Assert.NotNull(toolTip);
                Assert.Contains("数字化平台重构项目每周例会（腾讯会议 749-5361-1348）", toolTip);
                Assert.Contains("研发中心三楼会议室", toolTip);
                Assert.Contains("14:00", toolTip);
                Assert.Contains("15:00", toolTip);

                // 悬停空闲格子不应弹详情。
                board.UpdateHoverToolTip(new Point(
                    RoomBookingBoardControl.RoomWidth / 2,
                    (20 - RoomBookingBoardControl.FirstVisibleCell + 0.5) * RoomBookingBoardControl.CellHeight));
                Assert.Null(board.ToolTip as string);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DateBarUsesDateTextAndExposesExportButton()
    {
        RunOnSta(() =>
        {
            var picker = new RoomBookingPicker();
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            picker.Initialize(
                today,
                _ => Array.Empty<CalendarEvent>(),
                TimeZoneInfo.Local);

            Assert.Contains("(今天)", picker.DateText.Text, StringComparison.Ordinal);
            Assert.Equal(Visibility.Visible, picker.ExportButton.Visibility);

            picker.Initialize(
                today.AddDays(1),
                _ => Array.Empty<CalendarEvent>(),
                TimeZoneInfo.Local);

            Assert.DoesNotContain("(今天)", picker.DateText.Text, StringComparison.Ordinal);
            Assert.Equal(Visibility.Visible, picker.ExportButton.Visibility);
        });
    }

    private static Color GetPixel(BitmapSource bitmap, int x, int y)
    {
        var pixels = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
        return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
    }

    private static bool IsSelfOrDescendant(DependencyObject node, DependencyObject target)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, target))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private static void DoEvents(Window window)
    {
        window.Dispatcher.Invoke(DispatcherPriority.Background, () => { });
        window.Dispatcher.Invoke(DispatcherPriority.Render, () => { });
        window.Dispatcher.Invoke(DispatcherPriority.Loaded, () => { });
    }

    private static void RunOnSta(Action action) => WpfRuntimeHost.Run(action);
}
