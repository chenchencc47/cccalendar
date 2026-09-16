using System.Windows.Media;

namespace CcCalendar.Desktop.ViewModels;

/// <summary>
/// 会议室看板的绘制色板（P60-03）。
///
/// 这是纯数据记录：只做两件事——提供与主题令牌键一一对应的默认值，
/// 以及把任意「键 → 颜色」解析器合并进来。它不接触 WPF 资源字典，
/// 因此可以在没有 <see cref="System.Windows.Application"/> 的单元测试里直接断言，
/// 也避免了在渲染路径上做字符串反射。
///
/// 默认值必须与 <c>Themes/Theme.xaml</c> 的浅色令牌逐值一致，
/// 否则在解析器不可用时（设计器、无 Application 的宿主）看板会换个颜色。
/// </summary>
public readonly record struct RoomBoardPalette(
    Color GridSurface,
    Color GridHour,
    Color GridHalfHour,
    Color OccupiedFill,
    Color OccupiedText,
    Color OwnedFill,
    Color OwnedText,
    Color SelectedFreeFill,
    Color SelectedFreeText,
    Color ConflictFill,
    Color ConflictText,
    Color HandleFill,
    Color PreviewFill)
{
    /// <summary>令牌键前缀，供解析器与契约测试共用。</summary>
    public const string TokenPrefix = "RoomBoard";

    /// <summary>令牌键后缀：看板令牌都是画刷。</summary>
    public const string TokenSuffix = "Brush";

    /// <summary>浅色默认值：与 Theme.xaml 中 RoomBoard* 令牌逐值一致。</summary>
    public static RoomBoardPalette Light => new(
        GridSurface: Parse("#FFFFFF"),
        GridHour: Parse("#D9DEE5"),
        GridHalfHour: Parse("#F0F2F5"),
        OccupiedFill: Parse("#E4E7EA"),
        OccupiedText: Parse("#626A75"),
        OwnedFill: Parse("#DCEBFF"),
        OwnedText: Parse("#174A8B"),
        SelectedFreeFill: Parse("#D8F0DF"),
        SelectedFreeText: Parse("#1F5B3A"),
        ConflictFill: Parse("#F9E0E3"),
        ConflictText: Parse("#8F2231"),
        HandleFill: Parse("#FFFFFF"),
        PreviewFill: Parse("#33246BCE"));

    /// <summary>
    /// 用解析器覆写各分量。解析器收到的是**完整令牌键**（如 <c>RoomBoardOccupiedFill</c>），
    /// 因此调用方可以直接拿它去查资源字典。返回 <c>null</c> 时保留当前值，
    /// 资源缺失只会退回默认色而不会让看板画不出来。
    /// </summary>
    public RoomBoardPalette WithResolved(Func<string, Color?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);

        return new RoomBoardPalette(
            GridSurface: resolve(TokenKey(nameof(GridSurface))) ?? GridSurface,
            GridHour: resolve(TokenKey(nameof(GridHour))) ?? GridHour,
            GridHalfHour: resolve(TokenKey(nameof(GridHalfHour))) ?? GridHalfHour,
            OccupiedFill: resolve(TokenKey(nameof(OccupiedFill))) ?? OccupiedFill,
            OccupiedText: resolve(TokenKey(nameof(OccupiedText))) ?? OccupiedText,
            OwnedFill: resolve(TokenKey(nameof(OwnedFill))) ?? OwnedFill,
            OwnedText: resolve(TokenKey(nameof(OwnedText))) ?? OwnedText,
            SelectedFreeFill: resolve(TokenKey(nameof(SelectedFreeFill))) ?? SelectedFreeFill,
            SelectedFreeText: resolve(TokenKey(nameof(SelectedFreeText))) ?? SelectedFreeText,
            ConflictFill: resolve(TokenKey(nameof(ConflictFill))) ?? ConflictFill,
            ConflictText: resolve(TokenKey(nameof(ConflictText))) ?? ConflictText,
            HandleFill: resolve(TokenKey(nameof(HandleFill))) ?? HandleFill,
            PreviewFill: resolve(TokenKey(nameof(PreviewFill))) ?? PreviewFill);
    }

    /// <summary>
    /// 把 <c>GridSurface</c> 这样的分量名映射到完整令牌键
    /// （<c>RoomBoardGridSurfaceBrush</c>）。
    ///
    /// 令牌键统一以 <c>Brush</c> 结尾，与 Theme.xaml 中的声明保持一致；
    /// 这里由常量派生而不是手写字符串，避免两边拼写漂移。
    /// </summary>
    public static string TokenKey(string component) => $"{TokenPrefix}{component}{TokenSuffix}";

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
