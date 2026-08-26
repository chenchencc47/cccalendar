using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class RoomBookingBoardTests
{
    private static readonly TimeZoneInfo Zone = TimeSpan.FromHours(8) == TimeSpan.Zero
        ? TimeZoneInfo.Utc
        : TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");

    private static DateOnly Date => new(2026, 8, 19);

    [Fact]
    public void SnapsTimeToHalfHourCells()
    {
        Assert.Equal(0, RoomBookingBoard.TimeToCell(new TimeOnly(0, 0)));
        Assert.Equal(1, RoomBookingBoard.TimeToCell(new TimeOnly(0, 35)));
        Assert.Equal(28, RoomBookingBoard.TimeToCell(new TimeOnly(14, 0)));
        Assert.Equal(29, RoomBookingBoard.TimeToCell(new TimeOnly(14, 30)));
        Assert.Equal(30, RoomBookingBoard.TimeToCell(new TimeOnly(15, 10)));
        Assert.Equal(new TimeOnly(14, 30), RoomBookingBoard.CellToStartTime(29));
    }

    [Fact]
    public void BookingMarksOccupiedCellsForItsRoomOnly()
    {
        var board = new RoomBookingBoard(
            Date,
            ["聚英堂会议室", "技术中心二楼会议室"],
            [new RoomBookingOccurrence(
                "聚英堂会议室",
                new DateTimeOffset(2026, 8, 19, 10, 10, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 19, 11, 10, 0, TimeSpan.FromHours(8)))],
            Zone);

        // 10:10–11:10 占用 10:00、10:30、11:00 三个半小时格。
        Assert.True(board.IsOccupied("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(10, 0))));
        Assert.True(board.IsOccupied("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(10, 30))));
        Assert.True(board.IsOccupied("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(11, 0))));
        Assert.False(board.IsOccupied("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(11, 30))));
        Assert.False(board.IsOccupied("技术中心二楼会议室", RoomBookingBoard.TimeToCell(new TimeOnly(10, 0))));
    }

    [Fact]
    public void BookingOutsideBoardDateIsIgnored()
    {
        var board = new RoomBookingBoard(
            Date,
            ["聚英堂会议室"],
            [new RoomBookingOccurrence(
                "聚英堂会议室",
                new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.FromHours(8)))],
            Zone);

        Assert.Equal(RoomCellState.Unselected, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(10, 0))));
    }

    [Fact]
    public void SelectRangeMarksFreeCellsGreenAndConflictCellsRed()
    {
        var board = new RoomBookingBoard(
            Date,
            ["聚英堂会议室"],
            [new RoomBookingOccurrence(
                "聚英堂会议室",
                new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 19, 15, 0, 0, TimeSpan.FromHours(8)))],
            Zone);

        // 选择 14:00–16:00：14:00/14:30 冲突（红），15:00/15:30 空闲（绿）。
        board.SelectRange("聚英堂会议室", new TimeOnly(14, 0), new TimeOnly(16, 0));

        Assert.Equal(RoomCellState.SelectedConflict, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 0))));
        Assert.Equal(RoomCellState.SelectedConflict, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 30))));
        Assert.Equal(RoomCellState.SelectedFree, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(15, 0))));
        Assert.Equal(RoomCellState.SelectedFree, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(15, 30))));
    }

    [Fact]
    public void UnselectedOccupiedCellsAreGray()
    {
        var board = new RoomBookingBoard(
            Date,
            ["聚英堂会议室"],
            [new RoomBookingOccurrence(
                "聚英堂会议室",
                new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.FromHours(8)))],
            Zone);

        Assert.Equal(RoomCellState.Occupied, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(9, 0))));
        Assert.Equal(RoomCellState.Occupied, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(9, 30))));
        Assert.Equal(RoomCellState.Unselected, board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(11, 0))));
    }

    [Fact]
    public void HalfHourAlignedSelectionRangeIsExposedAsPrimarySelection()
    {
        var board = new RoomBookingBoard(Date, ["聚英堂会议室"], [], Zone);

        board.SelectRange("聚英堂会议室", new TimeOnly(14, 30), new TimeOnly(15, 30));

        Assert.True(board.TryGetPrimarySelection(out string room, out TimeOnly start, out TimeOnly end, out bool isFree));
        Assert.Equal("聚英堂会议室", room);
        Assert.Equal(new TimeOnly(14, 30), start);
        Assert.Equal(new TimeOnly(15, 30), end);
        Assert.True(isFree);
    }

    [Fact]
    public void PrimarySelectionReportsConflictWhenAnyCellOccupied()
    {
        var board = new RoomBookingBoard(
            Date,
            ["聚英堂会议室"],
            [new RoomBookingOccurrence(
                "聚英堂会议室",
                new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 19, 14, 30, 0, TimeSpan.FromHours(8)))],
            Zone);

        board.SelectRange("聚英堂会议室", new TimeOnly(14, 0), new TimeOnly(15, 0));

        Assert.True(board.TryGetPrimarySelection(out _, out _, out _, out bool isFree));
        Assert.False(isFree);
    }

    [Fact]
    public void RectDragSelectsBatchAcrossRoomsAndAccumulates()
    {
        var board = new RoomBookingBoard(
            Date,
            ["项目组二楼会议室", "聚英堂会议室", "技术中心二楼会议室"],
            [],
            Zone);

        // 拖一个矩形：聚英堂 14:00–15:00 + 技术中心 14:00–15:00。
        board.SelectRect("聚英堂会议室", new TimeOnly(14, 0), "技术中心二楼会议室", new TimeOnly(15, 0));

        Assert.Equal(RoomCellState.SelectedFree,
            board.GetCellState("聚英堂会议室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 30))));
        Assert.Equal(RoomCellState.SelectedFree,
            board.GetCellState("技术中心二楼会议室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 30))));
        Assert.Equal(RoomCellState.Unselected,
            board.GetCellState("项目组二楼会议室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 30))));

        // 再拉一个框：批量选择是累加的。
        board.SelectRect("项目组二楼会议室", new TimeOnly(9, 0), "项目组二楼会议室", new TimeOnly(9, 30));
        Assert.Equal(RoomCellState.SelectedFree,
            board.GetCellState("项目组二楼会议室", RoomBookingBoard.TimeToCell(new TimeOnly(9, 0))));
    }

    [Fact]
    public void RectDragNormalizesReversedDirection()
    {
        var board = new RoomBookingBoard(Date, ["A室", "B室"], [], Zone);

        board.SelectRect("B室", new TimeOnly(15, 30), "A室", new TimeOnly(14, 0));

        Assert.Equal(RoomCellState.SelectedFree, board.GetCellState("A室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 0))));
        Assert.Equal(RoomCellState.SelectedFree, board.GetCellState("B室", RoomBookingBoard.TimeToCell(new TimeOnly(15, 0))));
    }

    [Fact]
    public void ToggleCellRemovesSingleSelectedCell()
    {
        var board = new RoomBookingBoard(Date, ["A室"], [], Zone);

        board.SelectRange("A室", new TimeOnly(14, 0), new TimeOnly(15, 0));
        board.ToggleCell("A室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 30)));

        Assert.Equal(RoomCellState.SelectedFree, board.GetCellState("A室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 0))));
        Assert.Equal(RoomCellState.Unselected, board.GetCellState("A室", RoomBookingBoard.TimeToCell(new TimeOnly(14, 30))));
        Assert.Equal(RoomCellState.Unselected, board.GetCellState("A室", RoomBookingBoard.TimeToCell(new TimeOnly(15, 0))));
    }

    [Fact]
    public void ClearSelectionResetsAllCells()
    {
        var board = new RoomBookingBoard(Date, ["A室"], [], Zone);
        board.SelectRange("A室", new TimeOnly(9, 0), new TimeOnly(10, 0));

        board.ClearSelection();

        Assert.False(board.HasSelection);
        Assert.Equal(RoomCellState.Unselected, board.GetCellState("A室", RoomBookingBoard.TimeToCell(new TimeOnly(9, 0))));
    }

    [Fact]
    public void MergedSelectionSpansGroupContiguousCells()
    {
        var board = new RoomBookingBoard(Date, ["A室"], [], Zone);

        board.SelectRange("A室", new TimeOnly(9, 0), new TimeOnly(10, 0));
        board.SelectRange("A室", new TimeOnly(14, 0), new TimeOnly(15, 0));
        board.ToggleCell("A室", RoomBookingBoard.TimeToCell(new TimeOnly(11, 0)));

        var spans = board.GetSelectionSpans("A室").OrderBy(span => span.StartCell).ToArray();
        Assert.Equal(3, spans.Length);
        Assert.Equal(new TimeOnly(9, 0), RoomBookingBoard.CellToStartTime(spans[0].StartCell));
        Assert.Equal(new TimeOnly(11, 0), RoomBookingBoard.CellToStartTime(spans[1].StartCell));
        Assert.Equal(new TimeOnly(14, 0), RoomBookingBoard.CellToStartTime(spans[2].StartCell));
    }

    [Fact]
    public void IsRangeFreeReportsRoomAvailability()
    {
        var board = new RoomBookingBoard(
            Date,
            ["A室"],
            [new RoomBookingOccurrence(
                "A室",
                new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 19, 14, 30, 0, TimeSpan.FromHours(8)))],
            Zone);

        Assert.False(board.IsRangeFree("A室", new TimeOnly(14, 0), new TimeOnly(15, 0)));
        Assert.True(board.IsRangeFree("A室", new TimeOnly(14, 30), new TimeOnly(15, 30)));
    }

    [Fact]
    public void OvernightBookingClipsToBoardDateCells()
    {
        var board = new RoomBookingBoard(
            Date,
            ["A室"],
            [new RoomBookingOccurrence(
                "A室",
                new DateTimeOffset(2026, 8, 19, 23, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 20, 1, 0, 0, TimeSpan.FromHours(8)))],
            Zone);

        Assert.True(board.IsOccupied("A室", RoomBookingBoard.TimeToCell(new TimeOnly(23, 0))));
        Assert.True(board.IsOccupied("A室", RoomBookingBoard.TimeToCell(new TimeOnly(23, 30))));
        Assert.False(board.IsOccupied("A室", RoomBookingBoard.TimeToCell(new TimeOnly(0, 0))));
    }

    [Fact]
    public void ResizeSpanAdjustsBoundariesByHalfHourCells()
    {
        var board = new RoomBookingBoard(Date, ["A室"], [], Zone);
        board.SelectRange("A室", new TimeOnly(14, 0), new TimeOnly(15, 0));
        RoomSelectionSpan span = Assert.Single(board.GetSelectionSpans("A室"));

        board.ResizeSpan("A室", span, RoomBookingBoard.TimeToCell(new TimeOnly(14, 30)), RoomBookingBoard.TimeToCell(new TimeOnly(15, 30)));

        RoomSelectionSpan resized = Assert.Single(board.GetSelectionSpans("A室"));
        Assert.Equal(new TimeOnly(14, 30), RoomBookingBoard.CellToStartTime(resized.StartCell));
        Assert.Equal(new TimeOnly(15, 30), RoomBookingBoard.CellToStartTime(resized.EndCellExclusive - 1).Add(TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void GetClippedCellsClipsToBoardDate()
    {
        (int startCell, int endCellExclusive) = RoomBookingBoard.GetClippedCells(
            Date,
            new DateTimeOffset(2026, 8, 19, 23, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 20, 1, 0, 0, TimeSpan.FromHours(8)),
            Zone);
        Assert.Equal(46, startCell);
        Assert.Equal(48, endCellExclusive);

        (startCell, endCellExclusive) = RoomBookingBoard.GetClippedCells(
            Date,
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.FromHours(8)),
            Zone);
        Assert.Equal(0, startCell);
        Assert.Equal(0, endCellExclusive);
    }
}
