using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public readonly record struct RoomOccupiedBlock(
    string Room,
    int StartCell,
    int EndCellExclusive,
    string Title,
    TimeOnly Start,
    TimeOnly End,
    Guid? EventId = null,
    string? MeetingInvitationText = null,
    string? MeetingNumber = null,
    string? JoinUrl = null,
    string? OrganizerName = null,
    Guid? OrganizerId = null,
    bool IsOwnedByCurrentUser = false,
    bool IsRemoteBooking = false,
    DateOnly Date = default);

/// <summary>
/// 会议室时间视图区的自绘面板：8:00–24:00 半小时网格（白色底）、灰/绿/红色块、
/// 选中块四角白色调节点；支持点选、跨会议室拖选与上下边缘半小时调整；
/// 默认新选区替换旧选区，按住 Ctrl 累加多选。
/// </summary>
public sealed class RoomBookingBoardControl : FrameworkElement
{
    public const double CellHeight = 18;
    public const double HourHeight = CellHeight * 2;
    public const double RoomWidth = 125;
    /// <summary>首个可见小格：8:00（上班时间，之前的时段不展示）。</summary>
    public const int FirstVisibleCell = 16;
    /// <summary>可见小格数：8:00–24:00 共 16 小时。</summary>
    public const int VisibleCells = RoomBookingBoard.CellsPerDay - FirstVisibleCell;
    /// <summary>本人占用块的左侧标记条宽度，与导航选中项的 3px 标记一致。</summary>
    public const double AccentBarWidth = 3;
    /// <summary>
    /// 块标签的最小高度（一个小格）。标签字号为 12px（UI_DESIGN §2.1 辅助信息档），
    /// 因此至少要有一个整格的 18px 才放得下。
    /// </summary>
    public const double BlockLabelMinimumHeight = CellHeight;
    /// <summary>块标签字号，取自主题令牌 FontCaptionSize（UI_DESIGN §2.1）。</summary>
    private const double BlockLabelFontSize = 12;
    private const double EdgeHitRadius = 5;
    private const double HandleRadius = 4;

    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");

    private RoomBoardPalette palette = RoomBoardPalette.Light;
    private RoomBoardBrushes? brushes;

    /// <summary>
    /// 按当前主题解析看板色板，并丢弃缓存画刷。
    ///
    /// 资源查找从本控件向上走视觉树，因此必须在控件已挂到窗口之后调用才能读到
    /// 窗口级覆写（桌面组件的外观设置就是这样注入的）。找不到键时
    /// <see cref="RoomBoardPalette.WithResolved"/> 保留默认色，不会画出空色。
    /// </summary>
    public void RefreshPalette()
    {
        RoomBoardPalette resolved = RoomBoardPalette.Light.WithResolved(ResolvePaletteColor);
        if (resolved != palette || brushes is null)
        {
            palette = resolved;
            brushes = null;
            InvalidateVisual();
        }
    }

    private Color? ResolvePaletteColor(string key)
        => TryFindResource(key) is SolidColorBrush brush ? brush.Color : null;

    private RoomBoardBrushes Brushes => brushes ??= CreateBrushes(palette);

    private static RoomBoardBrushes CreateBrushes(RoomBoardPalette source) => new(
        GridSurface: CreateFrozenBrush(source.GridSurface),
        GridHour: CreateFrozenPen(source.GridHour),
        GridHalfHour: CreateFrozenPen(source.GridHalfHour),
        RoomBorder: CreateFrozenPen(source.GridHour),
        OccupiedFill: CreateFrozenBrush(source.OccupiedFill),
        OccupiedText: CreateFrozenBrush(source.OccupiedText),
        OwnedFill: CreateFrozenBrush(source.OwnedFill),
        OwnedText: CreateFrozenBrush(source.OwnedText),
        SelectedFreeFill: CreateFrozenBrush(source.SelectedFreeFill),
        SelectedFreeBorder: CreateFrozenPen(source.SelectedFreeText),
        SelectedFreeText: CreateFrozenBrush(source.SelectedFreeText),
        ConflictFill: CreateFrozenBrush(source.ConflictFill),
        ConflictBorder: CreateFrozenPen(source.ConflictText),
        ConflictText: CreateFrozenBrush(source.ConflictText),
        HandleFill: CreateFrozenBrush(source.HandleFill),
        HandleBorder: CreateFrozenPen(source.SelectedFreeText),
        PreviewFill: CreateFrozenBrush(source.PreviewFill),
        PreviewBorder: CreateFrozenPen(source.SelectedFreeText),
        AccentBar: CreateFrozenBrush(source.AccentBar));

    /// <summary>看板绘制用的画刷与画笔，由 <see cref="RoomBoardPalette"/> 派生。</summary>
    private sealed record RoomBoardBrushes(
        Brush GridSurface,
        Pen GridHour,
        Pen GridHalfHour,
        Pen RoomBorder,
        Brush OccupiedFill,
        Brush OccupiedText,
        Brush OwnedFill,
        Brush OwnedText,
        Brush SelectedFreeFill,
        Pen SelectedFreeBorder,
        Brush SelectedFreeText,
        Brush ConflictFill,
        Pen ConflictBorder,
        Brush ConflictText,
        Brush HandleFill,
        Pen HandleBorder,
        Brush PreviewFill,
        Pen PreviewBorder,
        Brush AccentBar);

    private RoomBookingBoard? board;
    private IReadOnlyList<RoomOccupiedBlock> occupiedBlocks = [];
    private IReadOnlyList<string> rooms = [];
    private double pixelsPerDip = 1;

    private Point dragAnchor;
    private Point dragCurrent;
    private bool isDragging;
    private string? resizeRoom;
    private RoomSelectionSpan resizeSpan;
    private bool resizeTop;

    /// <summary>用户提交了新的选择（点选/拖选/调整后触发）。</summary>
    public event EventHandler? SelectionChanged;

    public event EventHandler<RoomOccupiedBlock>? BookingDoubleClicked;

    public RoomBookingBoard? Board => board;

    internal IReadOnlyList<RoomOccupiedBlock> OccupiedBlocks => occupiedBlocks;

    public void SetContent(
        RoomBookingBoard? newBoard,
        IReadOnlyList<RoomOccupiedBlock> blocks,
        IReadOnlyList<string> roomNames)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(roomNames);
        board = newBoard;
        occupiedBlocks = blocks;
        rooms = roomNames;
        isDragging = false;
        resizeRoom = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double width = rooms.Count * RoomWidth;
        double height = VisibleCells * CellHeight;
        if (board is null || width <= 0)
        {
            return;
        }

        // 读到窗口级主题令牌后再画：桌面组件的外观设置就是这样注入到窗口资源的，
        // 因此必须在渲染时解析而不是在构造时固定。
        RefreshPalette();

        DrawGrid(drawingContext, width, height);
        DrawOccupiedBlocks(drawingContext);
        DrawSelectedSpans(drawingContext);
        if (isDragging)
        {
            DrawPreviewRect(drawingContext);
        }
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) =>
        new PointHitTestResult(this, hitTestParameters.HitPoint);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (board is null || rooms.Count == 0)
        {
            return;
        }

        Point position = e.GetPosition(this);
        (int roomIndex, int cellIndex) = HitTest(position);
        string room = rooms[roomIndex];
        if (e.ClickCount >= 2 && FindOccupiedBlock(room, cellIndex) is { } doubleClicked)
        {
            BookingDoubleClicked?.Invoke(this, doubleClicked);
            e.Handled = true;
            return;
        }

        if (FindResizeEdge(position, room, out RoomSelectionSpan span, out bool top))
        {
            resizeRoom = room;
            resizeSpan = span;
            resizeTop = top;
            isDragging = false;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        dragAnchor = position;
        dragCurrent = position;
        isDragging = true;
        // 不按 Ctrl 时新选区替换旧选区；按住 Ctrl 才是累加多选。
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            board.ClearSelection();
        }

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (board is null || rooms.Count == 0)
        {
            return;
        }

        Point position = e.GetPosition(this);
        if (resizeRoom is not null)
        {
            UpdateResize(position);
            e.Handled = true;
            return;
        }

        if (isDragging)
        {
            dragCurrent = position;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        UpdateResizeCursor(position);
        UpdateHoverToolTip(position);
    }

    /// <summary>
    /// 悬停在已占用格上时，用 ToolTip 展示该会议的主题、会议室与起止时间
    /// （本地日程标题含腾讯会议号时会一并显示）；悬停空闲区域清除提示。
    /// </summary>
    internal void UpdateHoverToolTip(Point position)
    {
        if (board is null || rooms.Count == 0)
        {
            ToolTip = null;
            return;
        }

        (int roomIndex, int cellIndex) = HitTest(position);
        string room = rooms[roomIndex];
        RoomOccupiedBlock? hovered = FindOccupiedBlock(room, cellIndex);

        ToolTip = hovered is { } meeting
            ? BuildMeetingToolTip(meeting)
            : null;
    }

    private static string BuildMeetingToolTip(RoomOccupiedBlock meeting)
    {
        string details = $"{meeting.Title}\n{meeting.Room}  {meeting.Start:HH\\:mm}–{meeting.End:HH\\:mm}";
        if (!string.IsNullOrWhiteSpace(meeting.MeetingNumber))
        {
            details += $"\n会议号：{meeting.MeetingNumber}";
        }

        if (!string.IsNullOrWhiteSpace(meeting.JoinUrl))
        {
            details += $"\n{meeting.JoinUrl}";
        }

        if (!string.IsNullOrWhiteSpace(meeting.OrganizerName))
        {
            details += $"\n会议人员：{meeting.OrganizerName}";
        }

        return details;
    }

    private RoomOccupiedBlock? FindOccupiedBlock(string room, int cellIndex)
    {
        foreach (RoomOccupiedBlock block in occupiedBlocks)
        {
            if (block.Room == room
                && cellIndex >= block.StartCell
                && cellIndex < block.EndCellExclusive)
            {
                return block;
            }
        }

        return null;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (board is null || rooms.Count == 0)
        {
            return;
        }

        if (resizeRoom is not null)
        {
            resizeRoom = null;
            ReleaseMouseCapture();
            RaiseSelectionChanged();
            e.Handled = true;
            return;
        }

        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        ReleaseMouseCapture();
        (int anchorRoom, int anchorCell) = HitTest(dragAnchor);
        (int currentRoom, int currentCell) = HitTest(dragCurrent);
        bool moved = currentRoom != anchorRoom || currentCell != anchorCell;
        bool additive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!additive)
        {
            board.ClearSelection();
        }

        if (moved)
        {
            board.SelectRect(
                rooms[anchorRoom],
                RoomBookingBoard.CellToStartTime(anchorCell),
                rooms[currentRoom],
                RoomBookingBoard.CellToStartTime(currentCell));
        }
        else
        {
            board.ToggleCell(rooms[anchorRoom], anchorCell);
        }

        InvalidateVisual();
        RaiseSelectionChanged();
        e.Handled = true;
    }

    private void UpdateResize(Point position)
    {
        int cell = HitTest(position).CellIndex;
        int newStart = resizeTop ? cell : resizeSpan.StartCell;
        int newEndExclusive = resizeTop ? resizeSpan.EndCellExclusive : cell + 1;
        if (newEndExclusive > newStart)
        {
            board!.ResizeSpan(resizeRoom!, resizeSpan, newStart, newEndExclusive);
            resizeSpan = board.GetSelectionSpans(resizeRoom!)
                .FirstOrDefault(span => span.StartCell <= newStart && span.EndCellExclusive >= newEndExclusive);
            InvalidateVisual();
        }
    }

    private void UpdateResizeCursor(Point position)
    {
        (int roomIndex, int _) = HitTest(position);
        string room = rooms[roomIndex];
        Cursor = FindResizeEdge(position, room, out _, out _)
            ? Cursors.SizeNS
            : null;
    }

    private bool FindResizeEdge(
        Point position,
        string room,
        out RoomSelectionSpan span,
        out bool top)
    {
        foreach (RoomSelectionSpan candidate in board!.GetSelectionSpans(room))
        {
            double spanTop = CellToY(candidate.StartCell);
            double spanBottom = CellToY(candidate.EndCellExclusive);
            if (Math.Abs(position.Y - spanTop) <= EdgeHitRadius)
            {
                span = candidate;
                top = true;
                return true;
            }

            if (Math.Abs(position.Y - spanBottom) <= EdgeHitRadius)
            {
                span = candidate;
                top = false;
                return true;
            }
        }

        span = default;
        top = false;
        return false;
    }

    private (int RoomIndex, int CellIndex) HitTest(Point position)
    {
        int roomIndex = rooms.Count == 0
            ? 0
            : Math.Clamp((int)(position.X / RoomWidth), 0, rooms.Count - 1);
        int cellIndex = Math.Clamp(
            FirstVisibleCell + (int)(position.Y / CellHeight),
            FirstVisibleCell,
            RoomBookingBoard.CellsPerDay - 1);
        return (roomIndex, cellIndex);
    }

    private static double CellToY(int cellIndex) => (cellIndex - FirstVisibleCell) * CellHeight;

    private void DrawGrid(DrawingContext drawingContext, double width, double height)
    {
        drawingContext.DrawRectangle(Brushes.GridSurface, null, new Rect(0, 0, width, height));
        for (int hour = 0; hour <= VisibleCells / 2; hour++)
        {
            double y = hour * 2 * CellHeight;
            drawingContext.DrawLine(Brushes.GridHour, new Point(0, y), new Point(width, y));
            // 半小时位置画浅色短线，让半小时选择粒度可见（含整点之间的 :30 分界）。
            if (hour < VisibleCells / 2)
            {
                drawingContext.DrawLine(
                    Brushes.GridHalfHour,
                    new Point(0, y + CellHeight),
                    new Point(width, y + CellHeight));
            }
        }

        for (int roomIndex = 1; roomIndex <= rooms.Count; roomIndex++)
        {
            double x = roomIndex * RoomWidth;
            drawingContext.DrawLine(Brushes.RoomBorder, new Point(x, 0), new Point(x, height));
        }
    }

    private void DrawOccupiedBlocks(DrawingContext drawingContext)
    {
        foreach (RoomOccupiedBlock block in occupiedBlocks)
        {
            if (block.EndCellExclusive <= block.StartCell
                || block.EndCellExclusive <= FirstVisibleCell)
            {
                continue;
            }

            int roomIndex = rooms.ToList().IndexOf(block.Room);
            if (roomIndex < 0)
            {
                continue;
            }

            int clippedStart = Math.Max(block.StartCell, FirstVisibleCell);
            double top = CellToY(clippedStart);
            double height = (block.EndCellExclusive - clippedStart) * CellHeight;
            var rect = new Rect(roomIndex * RoomWidth + 1, top + 1, RoomWidth - 2, height - 2);
            Brush fill = block.IsOwnedByCurrentUser ? Brushes.OwnedFill : Brushes.OccupiedFill;
            Brush text = block.IsOwnedByCurrentUser ? Brushes.OwnedText : Brushes.OccupiedText;
            drawingContext.DrawRectangle(fill, null, rect);
            if (block.IsOwnedByCurrentUser)
            {
                // 本人/他人的区分不能只靠底色（UI_DESIGN §2.3）：左侧 3px 竖条，
                // 与导航选中项同一视觉语法；文字起点同步右移避免压住竖条。
                drawingContext.DrawRectangle(
                    Brushes.AccentBar,
                    null,
                    new Rect(rect.Left, rect.Top, AccentBarWidth, rect.Height));
            }

            if (height >= BlockLabelMinimumHeight)
            {
                DrawText(
                    drawingContext,
                    $"已占用 · {block.Title}",
                    rect,
                    text,
                    isBold: false,
                    leftInset: block.IsOwnedByCurrentUser ? AccentBarWidth : 0d);
            }
        }
    }

    private void DrawSelectedSpans(DrawingContext drawingContext)
    {
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            string room = rooms[roomIndex];
            foreach (RoomSelectionSpan span in board!.GetSelectionSpans(room))
            {
                bool allFree = true;
                for (int cell = span.StartCell; cell < span.EndCellExclusive; cell++)
                {
                    if (board.IsOccupied(room, cell))
                    {
                        allFree = false;
                        break;
                    }
                }

                double top = CellToY(span.StartCell);
                double height = (span.EndCellExclusive - span.StartCell) * CellHeight;
                var rect = new Rect(roomIndex * RoomWidth + 1, top + 1, RoomWidth - 2, height - 2);
                Brush fill = allFree ? Brushes.SelectedFreeFill : Brushes.ConflictFill;
                Pen border = allFree ? Brushes.SelectedFreeBorder : Brushes.ConflictBorder;
                drawingContext.DrawRoundedRectangle(fill, border, rect, 3, 3);
                if (height >= 18)
                {
                    string label = $"{room} {(allFree ? "空闲" : "已占用")}";
                    DrawText(
                        drawingContext,
                        label,
                        rect,
                        allFree ? Brushes.SelectedFreeText : Brushes.ConflictText,
                        isBold: true);
                }

                DrawCornerHandles(drawingContext, rect);
            }
        }
    }

    private void DrawCornerHandles(DrawingContext drawingContext, Rect rect)
    {
        Point[] corners =
        [
            new(rect.Left, rect.Top),
            new(rect.Right, rect.Top),
            new(rect.Left, rect.Bottom),
            new(rect.Right, rect.Bottom),
        ];
        foreach (Point corner in corners)
        {
            drawingContext.DrawEllipse(Brushes.HandleFill, Brushes.HandleBorder, corner, HandleRadius, HandleRadius);
        }
    }

    private void DrawPreviewRect(DrawingContext drawingContext)
    {
        (int anchorRoom, int anchorCell) = HitTest(dragAnchor);
        (int currentRoom, int currentCell) = HitTest(dragCurrent);
        int firstCell = Math.Min(anchorCell, currentCell);
        int lastCellExclusive = Math.Max(anchorCell, currentCell) + 1;
        int firstRoom = Math.Min(anchorRoom, currentRoom);
        int lastRoomExclusive = Math.Max(anchorRoom, currentRoom) + 1;
        var rect = new Rect(
            firstRoom * RoomWidth + 1,
            CellToY(firstCell) + 1,
            (lastRoomExclusive - firstRoom) * RoomWidth - 2,
            (lastCellExclusive - firstCell) * CellHeight - 2);
        drawingContext.DrawRectangle(Brushes.PreviewFill, Brushes.PreviewBorder, rect);
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        Rect bounds,
        Brush foreground,
        bool isBold,
        double leftInset = 0d)
    {
        double left = bounds.Left + 4 + leftInset;
        var formatted = new FormattedText(
            text,
            ChineseCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable, Microsoft YaHei UI"), FontStyles.Normal, isBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            BlockLabelFontSize,
            foreground,
            pixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, bounds.Width - 8 - leftInset),
            MaxTextHeight = Math.Max(1, bounds.Height - 4),
            Trimming = TextTrimming.CharacterEllipsis,
        };
        drawingContext.DrawText(formatted, new Point(left, bounds.Top + 2));
    }

    private void RaiseSelectionChanged()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozenPen(Color color)
    {
        var pen = new Pen(CreateFrozenBrush(color), 1);
        pen.Freeze();
        return pen;
    }
}
