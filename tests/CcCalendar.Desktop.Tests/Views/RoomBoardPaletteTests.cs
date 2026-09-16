using System.IO;
using System.Windows;
using System.Windows.Media;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// P60-03 会议室看板配色契约。
///
/// 看板是自绘控件，颜色不走 XAML 绑定，所以「令牌存在」不等于「看板用了令牌」。
/// 这组测试把两头都钉住：
/// 1. <see cref="RoomBoardPalette.Light"/> 的每个分量都必须与 Theme.xaml 的浅色令牌逐值一致；
/// 2. 深色字典必须覆写**全部**看板令牌（漏一个就会出现白底深色看板那种半调子状态）；
/// 3. 解析器返回 null 时必须退回默认色而不是画出空色。
/// </summary>
public sealed class RoomBoardPaletteTests
{
    private const string ThemeSource =
        "pack://application:,,,/cccalendar;component/Themes/Theme.xaml";
    private const string DarkThemeSource =
        "pack://application:,,,/cccalendar;component/Themes/DarkTheme.xaml";

    /// <summary>看板全部令牌键，按分量名逐一映射。</summary>
    private static readonly string[] BoardTokenComponents =
    [
        nameof(RoomBoardPalette.GridSurface),
        nameof(RoomBoardPalette.GridHour),
        nameof(RoomBoardPalette.GridHalfHour),
        nameof(RoomBoardPalette.OccupiedFill),
        nameof(RoomBoardPalette.OccupiedText),
        nameof(RoomBoardPalette.OwnedFill),
        nameof(RoomBoardPalette.OwnedText),
        nameof(RoomBoardPalette.SelectedFreeFill),
        nameof(RoomBoardPalette.SelectedFreeText),
        nameof(RoomBoardPalette.ConflictFill),
        nameof(RoomBoardPalette.ConflictText),
        nameof(RoomBoardPalette.HandleFill),
        nameof(RoomBoardPalette.PreviewFill),
    ];

    [Fact]
    public void LightPaletteMatchesThemeLightTokensExactly()
    {
        RunOnThemeThread((light, _) =>
        {
            RoomBoardPalette palette = RoomBoardPalette.Light;

            foreach (string component in BoardTokenComponents)
            {
                string key = RoomBoardPalette.TokenKey(component);
                Color expected = ReadColor(light, key);
                Color actual = ReadPaletteComponent(palette, component);

                Assert.True(
                    expected == actual,
                    $"看板令牌 '{key}' 与 RoomBoardPalette.Light.{component} 不一致："
                        + $"Theme.xaml={expected}，默认值={actual}。"
                        + "两者必须同步，否则资源不可用时会画出另一种颜色。");
            }
        });
    }

    [Fact]
    public void DarkThemeOverridesEveryBoardToken()
    {
        RunOnThemeThread((light, dark) =>
        {
            foreach (string component in BoardTokenComponents)
            {
                string key = RoomBoardPalette.TokenKey(component);

                Assert.True(light.Contains(key), $"看板令牌 '{key}' 未在 Theme.xaml 中声明。");
                Assert.True(
                    dark.Contains(key),
                    $"看板令牌 '{key}' 未在 DarkTheme.xaml 中覆写；"
                        + "深色主题下看板会沿用浅色值（这正是修复前的白底问题）。");

                Assert.True(
                    ReadColor(light, key) != ReadColor(dark, key),
                    $"看板令牌 '{key}' 在深色字典中与浅色值相同；"
                        + "深色下需要不同的亮度，否则状态不可读。");
            }
        });
    }

    [Fact]
    public void ResolverOverridesComponentsAndKeepsDefaultsForMissingKeys()
    {
        // 只提供一个键，其余保持默认 —— 模拟「部分资源缺失」。
        Color overrideColor = Color.FromRgb(0x12, 0x34, 0x56);
        RoomBoardPalette palette = RoomBoardPalette.Light.WithResolved(key =>
            key == RoomBoardPalette.TokenKey(nameof(RoomBoardPalette.OccupiedFill))
                ? overrideColor
                : null);

        Assert.Equal(overrideColor, palette.OccupiedFill);

        // 未解析到的分量必须保留默认值，不能变成透明或黑色。
        Assert.Equal(RoomBoardPalette.Light.OccupiedText, palette.OccupiedText);
        Assert.Equal(RoomBoardPalette.Light.GridSurface, palette.GridSurface);
        Assert.Equal(RoomBoardPalette.Light.ConflictText, palette.ConflictText);
    }

    [Fact]
    public void ResolverReceivesEveryBoardTokenKey()
    {
        var requested = new List<string>();
        _ = RoomBoardPalette.Light.WithResolved(key =>
        {
            requested.Add(key);
            return null;
        });

        string[] expected = [.. BoardTokenComponents.Select(RoomBoardPalette.TokenKey)];
        Assert.Equal(expected.OrderBy(k => k, StringComparer.Ordinal), requested.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>
    /// 按分量名取值。<paramref name="component"/> 是 <c>GridSurface</c> 这样的分量名，
    /// 不是完整令牌键；完整键由 <see cref="RoomBoardPalette.TokenKey"/> 派生。
    /// </summary>
    private static Color ReadPaletteComponent(RoomBoardPalette palette, string component) => component switch
    {
        nameof(RoomBoardPalette.GridSurface) => palette.GridSurface,
        nameof(RoomBoardPalette.GridHour) => palette.GridHour,
        nameof(RoomBoardPalette.GridHalfHour) => palette.GridHalfHour,
        nameof(RoomBoardPalette.OccupiedFill) => palette.OccupiedFill,
        nameof(RoomBoardPalette.OccupiedText) => palette.OccupiedText,
        nameof(RoomBoardPalette.OwnedFill) => palette.OwnedFill,
        nameof(RoomBoardPalette.OwnedText) => palette.OwnedText,
        nameof(RoomBoardPalette.SelectedFreeFill) => palette.SelectedFreeFill,
        nameof(RoomBoardPalette.SelectedFreeText) => palette.SelectedFreeText,
        nameof(RoomBoardPalette.ConflictFill) => palette.ConflictFill,
        nameof(RoomBoardPalette.ConflictText) => palette.ConflictText,
        nameof(RoomBoardPalette.HandleFill) => palette.HandleFill,
        nameof(RoomBoardPalette.PreviewFill) => palette.PreviewFill,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "未知的看板分量。"),
    };

    private static Color ReadColor(ResourceDictionary dictionary, string key)
    {
        Assert.True(dictionary.Contains(key), $"主题字典缺少令牌 '{key}'。");
        return Assert.IsType<SolidColorBrush>(dictionary[key]).Color;
    }

    private static void RunOnThemeThread(Action<ResourceDictionary, ResourceDictionary> assertion)
    {
        WpfRuntimeHost.Run(() =>
        {
            ResourceDictionary light = new() { Source = new Uri(ThemeSource) };
            ResourceDictionary dark = new() { Source = new Uri(DarkThemeSource) };
            assertion(light, dark);
        });
    }
}
