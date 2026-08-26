namespace CcCalendar.Desktop.ViewModels;

public enum RoomCellState
{
    Unselected,
    SelectedFree,
    SelectedConflict,
    Occupied,
}

public readonly record struct RoomBookingOccurrence(
    string Room,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc);

public readonly record struct RoomSelectionSpan(int StartCell, int EndCellExclusive);

/// <summary>
/// 会议室预订板核心状态：24 小时按半小时分格（48 格），
/// 支持半小时对齐的选择、跨会议室矩形批量选择与占用判定。
/// </summary>
public sealed class RoomBookingBoard
{
    public const int CellsPerDay = 48;

    private readonly HashSet<string> occupiedCells = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> selectedCells = new(StringComparer.Ordinal);

    public RoomBookingBoard(
        DateOnly date,
        IReadOnlyList<string> rooms,
        IEnumerable<RoomBookingOccurrence> bookings,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(bookings);
        ArgumentNullException.ThrowIfNull(timeZone);
        Date = date;
        Rooms = rooms;
        foreach (RoomBookingOccurrence booking in bookings)
        {
            MarkOccupied(booking, timeZone);
        }
    }

    public DateOnly Date { get; }

    public IReadOnlyList<string> Rooms { get; }

    public bool HasSelection => selectedCells.Count > 0;

    public static int TimeToCell(TimeOnly value)
    {
        return value.Hour * 2 + (value.Minute >= 30 ? 1 : 0);
    }

    public static TimeOnly CellToStartTime(int cellIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cellIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cellIndex, CellsPerDay);
        return new TimeOnly(cellIndex / 2, (cellIndex % 2) * 30);
    }

    public bool IsOccupied(string room, int cellIndex)
    {
        return occupiedCells.Contains(CreateCellKey(room, cellIndex));
    }

    public RoomCellState GetCellState(string room, int cellIndex)
    {
        if (IsOccupied(room, cellIndex))
        {
            return IsCellSelected(room, cellIndex)
                ? RoomCellState.SelectedConflict
                : RoomCellState.Occupied;
        }

        return IsCellSelected(room, cellIndex)
            ? RoomCellState.SelectedFree
            : RoomCellState.Unselected;
    }

    public void SelectRange(string room, TimeOnly start, TimeOnly end)
    {
        int startCell = TimeToCell(start);
        int endCellExclusive = Math.Clamp(CellExclusiveEnd(end), startCell + 1, CellsPerDay);
        HashSet<int> cells = GetRoomSelection(room);
        for (int cell = startCell; cell < endCellExclusive; cell++)
        {
            cells.Add(cell);
        }
    }

    public void SelectRect(string startRoom, TimeOnly startTime, string endRoom, TimeOnly endTime)
    {
        int startRoomIndex = IndexOfRoom(startRoom);
        int endRoomIndex = IndexOfRoom(endRoom);
        if (startRoomIndex < 0 || endRoomIndex < 0)
        {
            return;
        }

        int firstRoom = Math.Min(startRoomIndex, endRoomIndex);
        int lastRoom = Math.Max(startRoomIndex, endRoomIndex);
        int startCell = TimeToCell(startTime);
        int endCell = TimeToCell(endTime);
        int firstCell = Math.Min(startCell, endCell);
        int lastCell = Math.Max(startCell, endCell);

        for (int roomIndex = firstRoom; roomIndex <= lastRoom; roomIndex++)
        {
            HashSet<int> cells = GetRoomSelection(Rooms[roomIndex]);
            for (int cell = firstCell; cell <= lastCell; cell++)
            {
                cells.Add(cell);
            }
        }
    }

    public void ToggleCell(string room, int cellIndex)
    {
        HashSet<int> cells = GetRoomSelection(room);
        if (!cells.Remove(cellIndex))
        {
            cells.Add(cellIndex);
        }
    }

    public void ClearSelection()
    {
        selectedCells.Clear();
    }

    public void ResizeSpan(string room, RoomSelectionSpan span, int newStartCell, int newEndCellExclusive)
    {
        HashSet<int> cells = GetRoomSelection(room);
        for (int cell = span.StartCell; cell < span.EndCellExclusive; cell++)
        {
            cells.Remove(cell);
        }

        newStartCell = Math.Clamp(newStartCell, 0, CellsPerDay - 1);
        newEndCellExclusive = Math.Clamp(newEndCellExclusive, newStartCell + 1, CellsPerDay);
        for (int cell = newStartCell; cell < newEndCellExclusive; cell++)
        {
            cells.Add(cell);
        }
    }

    /// <summary>计算占用区间在指定日期内覆盖的半小时格（排他端点），跨天自动裁剪。</summary>
    public static (int StartCell, int EndCellExclusive) GetClippedCells(
        DateOnly boardDate,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        TimeZoneInfo timeZone)
    {
        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(startUtc, timeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(endUtc, timeZone);
        DateOnly startDate = DateOnly.FromDateTime(localStart.DateTime);
        DateOnly endDate = DateOnly.FromDateTime(localEnd.DateTime);
        if (startDate > boardDate || endDate < boardDate)
        {
            return (0, 0);
        }

        TimeOnly dayStart = startDate < boardDate ? TimeOnly.MinValue : TimeOnly.FromDateTime(localStart.DateTime);
        TimeOnly dayEnd = endDate > boardDate ? TimeOnly.MaxValue : TimeOnly.FromDateTime(localEnd.DateTime);
        int startCell = Math.Max(0, TimeToCell(dayStart));
        int endCellExclusive = Math.Min(CellsPerDay, CellExclusiveEnd(dayEnd));
        return (startCell, endCellExclusive);
    }

    public IReadOnlyList<RoomSelectionSpan> GetSelectionSpans(string room)
    {
        if (!selectedCells.TryGetValue(room, out HashSet<int>? cells) || cells.Count == 0)
        {
            return [];
        }

        var spans = new List<RoomSelectionSpan>();
        int[] ordered = [.. cells.OrderBy(cell => cell)];
        int start = ordered[0];
        int previous = ordered[0];
        foreach (int cell in ordered.Skip(1))
        {
            if (cell != previous + 1)
            {
                spans.Add(new RoomSelectionSpan(start, previous + 1));
                start = cell;
            }

            previous = cell;
        }

        spans.Add(new RoomSelectionSpan(start, previous + 1));
        return spans;
    }

    public bool IsRangeFree(string room, TimeOnly start, TimeOnly end)
    {
        int startCell = TimeToCell(start);
        int endCellExclusive = Math.Max(startCell + 1, CellExclusiveEnd(end));
        return !Enumerable.Range(startCell, endCellExclusive - startCell)
            .Any(cell => IsOccupied(room, cell));
    }

    public bool TryGetPrimarySelection(
        out string room,
        out TimeOnly start,
        out TimeOnly end,
        out bool isFree)
    {
        foreach (string candidate in Rooms)
        {
            RoomSelectionSpan span = GetSelectionSpans(candidate)
                .OrderBy(item => item.StartCell)
                .FirstOrDefault();
            if (span.EndCellExclusive == 0)
            {
                continue;
            }

            room = candidate;
            start = CellToStartTime(span.StartCell);
            end = CellToStartTime(span.EndCellExclusive - 1).Add(TimeSpan.FromMinutes(30));
            isFree = !Enumerable.Range(span.StartCell, span.EndCellExclusive - span.StartCell)
                .Any(cell => IsOccupied(candidate, cell));
            return true;
        }

        room = string.Empty;
        start = default;
        end = default;
        isFree = false;
        return false;
    }

    private void MarkOccupied(RoomBookingOccurrence booking, TimeZoneInfo timeZone)
    {
        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(booking.StartUtc, timeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(booking.EndUtc, timeZone);
        if (localEnd <= localStart)
        {
            return;
        }

        DateOnly startDate = DateOnly.FromDateTime(localStart.DateTime);
        DateOnly endDate = DateOnly.FromDateTime(localEnd.DateTime);
        if (startDate > Date || endDate < Date)
        {
            return;
        }

        TimeOnly dayStart = startDate < Date ? TimeOnly.MinValue : TimeOnly.FromDateTime(localStart.DateTime);
        TimeOnly dayEnd = endDate > Date ? TimeOnly.MaxValue : TimeOnly.FromDateTime(localEnd.DateTime);
        int startCell = Math.Max(0, TimeToCell(dayStart));
        int endCellExclusive = Math.Min(CellsPerDay, CellExclusiveEnd(dayEnd));
        for (int cell = startCell; cell < endCellExclusive; cell++)
        {
            occupiedCells.Add(CreateCellKey(booking.Room, cell));
        }
    }

    /// <summary>把结束时间转换为排他格下标；正好落在半小时边界不加格，跨入格内则占满该格。</summary>
    private static int CellExclusiveEnd(TimeOnly end)
    {
        int cell = TimeToCell(end);
        return end.Minute % 30 == 0 && end.Second == 0 ? cell : cell + 1;
    }

    private bool IsCellSelected(string room, int cellIndex)
    {
        return selectedCells.TryGetValue(room, out HashSet<int>? cells) && cells.Contains(cellIndex);
    }

    private HashSet<int> GetRoomSelection(string room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (!selectedCells.TryGetValue(room, out HashSet<int>? cells))
        {
            cells = new HashSet<int>();
            selectedCells[room] = cells;
        }

        return cells;
    }

    private int IndexOfRoom(string room) => Rooms.ToList().IndexOf(room);

    private static string CreateCellKey(string room, int cellIndex) => $"{room}|{cellIndex}";
}
