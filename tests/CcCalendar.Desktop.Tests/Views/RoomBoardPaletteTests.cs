using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

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
        nameof(RoomBoardPalette.AccentBar),
    ];

    /// <summary>
    /// 看板上的「文字/底色」对。每一对都必须满足 WCAG AA 的 4.5:1，
    /// 否则在会议室墙上的远距离阅读会不可辨（P60-04）。
    /// </summary>
    private static readonly (string Text, string Fill, string Label)[] TextPairs =
    [
        (
            nameof(RoomBoardPalette.OccupiedText),
            nameof(RoomBoardPalette.OccupiedFill),
            "他人占用"),
        (
            nameof(RoomBoardPalette.OwnedText),
            nameof(RoomBoardPalette.OwnedFill),
            "本人占用"),
        (
            nameof(RoomBoardPalette.SelectedFreeText),
            nameof(RoomBoardPalette.SelectedFreeFill),
            "选中空闲"),
        (
            nameof(RoomBoardPalette.ConflictText),
            nameof(RoomBoardPalette.ConflictFill),
            "选中冲突"),
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
        Assert.Equal(
            expected.OrderBy(k => k, StringComparer.Ordinal),
            requested.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>
    /// P60-04：看板文字必须达到 WCAG AA 的 4.5:1。
    ///
    /// 这条测试来自实测：初版浅色「他人占用」用 `#626A75` 配 `#E4E7EA`，对比度
    /// 只有 **4.41:1**，低于下限；其余三对分别 7.27/6.67/6.88 均达标。
    /// 会议室看板是远距离阅读场景，不能只看「看起来还行」。
    /// </summary>
    [Fact]
    public void BoardTextPairsMeetWcagAaContrast()
    {
        RunOnThemeThread((light, dark) =>
        {
            foreach ((ResourceDictionary dictionary, string themeName) in
                new[] { (light, "浅色"), (dark, "深色") })
            {
                foreach ((string textComponent, string fillComponent, string label) in TextPairs)
                {
                    Color text = ReadColor(dictionary, RoomBoardPalette.TokenKey(textComponent));
                    Color fill = ReadColor(dictionary, RoomBoardPalette.TokenKey(fillComponent));
                    double ratio = ColorContrast.Ratio(text, fill);

                    Assert.True(
                        ratio >= ColorContrast.TextMinimum,
                        $"{themeName}主题「{label}」的文字对比度 {ratio:F2}:1 低于 "
                            + $"{ColorContrast.TextMinimum}:1（文字 {text}，底色 {fill}）。");
                }
            }
        });
    }

    /// <summary>
    /// P60-04：本人/他人占用不能只靠底色区分（UI_DESIGN §2.3）。
    ///
    /// 断言两种主题下「本人占用」都有独立的标记条颜色，且与「他人占用」的底色不同——
    /// 这样即使把界面灰度化，本人预约仍有结构性的视觉差异。
    /// </summary>
    [Fact]
    public void OwnedBlocksCarryAMarkerIndependentOfFillColour()
    {
        RunOnThemeThread((light, dark) =>
        {
            foreach ((ResourceDictionary dictionary, string themeName) in
                new[] { (light, "浅色"), (dark, "深色") })
            {
                Color accentBar = ReadColor(dictionary, RoomBoardPalette.TokenKey(nameof(RoomBoardPalette.AccentBar)));
                Color occupiedFill = ReadColor(dictionary, RoomBoardPalette.TokenKey(nameof(RoomBoardPalette.OccupiedFill)));
                Color ownedFill = ReadColor(dictionary, RoomBoardPalette.TokenKey(nameof(RoomBoardPalette.OwnedFill)));

                Assert.True(
                    accentBar != occupiedFill,
                    $"{themeName}主题的本人标记条颜色与「他人占用」底色相同，无法区分。");

                // 标记条必须贴在本人占用的底色上仍然可见。
                double ratio = ColorContrast.Ratio(accentBar, ownedFill);
                Assert.True(
                    ratio >= ColorContrast.NonTextMinimum,
                    $"{themeName}主题本人标记条对本人底色的对比度 {ratio:F2}:1 低于 "
                        + $"{ColorContrast.NonTextMinimum}:1（标记 {accentBar}，底色 {ownedFill}）。");
            }
        });
    }

    /// <summary>
    /// 按分量名取值。<paramref name="component"/> 是 <c>GridSurface</c> 这样的分量名，
    /// 不是完整令牌键；完整键由 <see cref="RoomBoardPalette.TokenKey"/> 派生。
    /// </summary>
    /// <summary>
    /// P60-04：看板标签字号必须走令牌档位（UI_DESIGN §2.1 辅助信息 12px）。
    ///
    /// 修复前是硬编码 11px——既不在字号阶梯上，也低于看板远距离阅读所需。
    /// 同时断言标签高度门槛与字号配套，避免改了字号却忘了留高度。
    /// </summary>
    [Fact]
    public void BoardLabelFontSizeMatchesTheCaptionToken()
    {
        RunOnThemeThread((light, _) =>
        {
            double captionSize = Assert.IsType<double>(light["FontCaptionSize"]);

            // 控件对标签的可见性门槛必须能容纳该字号（一个小格 18px）。
            Assert.True(
                RoomBookingBoardControl.BlockLabelMinimumHeight >= captionSize,
                $"标签高度门槛 {RoomBookingBoardControl.BlockLabelMinimumHeight} 小于字号 {captionSize}，"
                    + "文字会被裁掉。");

            // 字号档位里必须有 12 这一档，且与标签字号一致。
            Assert.True(
                Math.Abs(captionSize - 12d) < 0.01d,
                $"FontCaptionSize 应为 12（UI_DESIGN §2.1），实际 {captionSize}。");
        });
    }

    /// <summary>
    /// P60-04：本人标记条宽度与导航选中项的 3px 标记一致。
    /// </summary>
    [Fact]
    public void AccentBarWidthMatchesTheNavigationMarker()
    {
        Assert.Equal(3d, RoomBookingBoardControl.AccentBarWidth);
    }

    /// <summary>
    /// P60-06 视觉验收辅助：把看板在浅色与深色下各渲染一张 PNG，输出到
    /// <c>artifacts/ui-p60/</c> 供人工核验。
    ///
    /// 这不是断言型测试（渲染结果由人看），因此除「文件确实写出且非空」之外不做判断；
    /// 它的价值是让「深色看板不再是白底」这件事有可复核的图像证据，而不只是日志里的一句话。
    /// </summary>
    [Fact]
    public void RendersBoardScreenshotsForManualReview()
    {
        string outputDirectory = Path.Combine(FindRepositoryRoot(), "artifacts", "ui-p60");
        Directory.CreateDirectory(outputDirectory);

        foreach ((string themeName, string? themeSource) in new (string, string?)[]
        {
            ("light", null),
            ("dark", "pack://application:,,,/cccalendar;component/Themes/DarkTheme.xaml"),
        })
        {
            RunOnThemeThread((_, __) =>
            {
                var window = new Window { Width = 760, Height = 640, ShowInTaskbar = false };
                if (themeSource is not null)
                {
                    window.Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = new Uri(themeSource) });
                }

                var picker = new RoomBookingPicker();
                picker.Initialize(
                    DateOnly.Parse("2026-08-20", CultureInfo.InvariantCulture),
                    _ => Array.Empty<CcCalendar.Core.Schedules.CalendarEvent>(),
                    TimeZoneInfo.Local);
                window.Content = picker;
                try
                {
                    window.Show();
                    window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, () => { });
                    window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, () => { });

                    RoomBookingBoardControl board = picker.Board;
                    board.RefreshPalette();
                    board.UpdateLayout();

                    int width = (int)Math.Ceiling(board.ActualWidth);
                    int height = (int)Math.Ceiling(board.ActualHeight);

                    // RenderTargetBitmap.Render(board) 会按控件在视觉树中的偏移作画
                    // （看板位于 44px 小时刻度列之后），因此截图左侧会留一条空带。
                    // 用 VisualBrush 包裹后从 (0,0) 重绘，把偏移抵消掉。
                    var wrapper = new DrawingVisual();
                    using (DrawingContext context = wrapper.RenderOpen())
                    {
                        context.DrawRectangle(
                            new VisualBrush(board) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                            null,
                            new Rect(0, 0, width, height));
                    }

                    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(wrapper);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    string path = Path.Combine(outputDirectory, $"room-board-{themeName}.png");
                    using FileStream stream = File.Create(path);
                    encoder.Save(stream);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        foreach (string themeName in new[] { "light", "dark" })
        {
            string path = Path.Combine(outputDirectory, $"room-board-{themeName}.png");
            Assert.True(File.Exists(path), $"未生成看板截图：{path}");
            Assert.True(new FileInfo(path).Length > 0, $"看板截图为空文件：{path}");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CcCalendar.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

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
        nameof(RoomBoardPalette.AccentBar) => palette.AccentBar,
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
