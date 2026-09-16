using System.IO;
using System.Windows;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// P60-01 设计令牌基线契约。
///
/// 主题令牌必须满足两个条件，缺一不可：
/// 1. 在 <c>Themes/Theme.xaml</c> 中有声明（浅色基准），且颜色类令牌在
///    <c>Themes/DarkTheme.xaml</c> 中有覆写；
/// 2. 声明能被 XAML 解析器真正读出（类型正确、键不重复）。
///
/// 纯文本断言无法覆盖第 2 点：一个键可以「写在文件里」但因为键重复、XML 非法
/// 或类型错误而在运行时不生效。因此这里在 STA 线程上真实加载两个资源字典来断言。
/// </summary>
public sealed class ThemeTokenContractTests
{
    private const string ThemeSource =
        "pack://application:,,,/cccalendar;component/Themes/Theme.xaml";
    private const string DarkThemeSource =
        "pack://application:,,,/cccalendar;component/Themes/DarkTheme.xaml";

    private const string ThemeRelativePath = "src/CcCalendar.Desktop/Themes/Theme.xaml";
    private const string DarkThemeRelativePath = "src/CcCalendar.Desktop/Themes/DarkTheme.xaml";

    /// <summary>间距令牌，UI_DESIGN §2.2 的 4/8/12/16/24 基线。</summary>
    private static readonly string[] SpacingTokens =
    [
        "SpacingXs",
        "SpacingSm",
        "SpacingMd",
        "SpacingLg",
        "SpacingXl",
    ];

    /// <summary>圆角令牌，上限 8px（UI_DESIGN §2.2）。</summary>
    private static readonly string[] RadiusTokens =
    [
        "RadiusSm",
        "RadiusMd",
        "RadiusLg",
    ];

    /// <summary>字号令牌，UI_DESIGN §2.1。</summary>
    private static readonly string[] FontSizeTokens =
    [
        "FontCaptionSize",
        "FontCompactSize",
        "FontPanelTitleSize",
        "FontPageTitleSize",
        "FontClockSize",
    ];

    /// <summary>控件尺寸令牌，UI_DESIGN §2.1/§2.2/§10。</summary>
    private static readonly string[] ControlSizeTokens =
    [
        "ControlHeightPrimary",
        "IconButtonSize",
        "SidebarWidth",
        "BrandBarHeight",
        "NavigationItemHeight",
    ];

    /// <summary>动效令牌，交互 120ms / 显隐 160ms，缓动统一。</summary>
    private static readonly string[] MotionDurationTokens =
    [
        "MotionFast",
        "MotionBase",
    ];

    /// <summary>缓动令牌。</summary>
    private static readonly string[] EasingTokens =
    [
        "EasingStandard",
    ];

    /// <summary>颜色令牌：浅色声明 + 深色覆写，值必须为 Brush。</summary>
    private static readonly string[] ColorTokens =
    [
        "ScrollbarThumbBrush",
        "ScrollbarThumbHoverBrush",
        "FocusRingBrush",
        "OverlayScrimBrush",
        "ElevationPanelBrush",
        "ElevationProminentBrush",
    ];

    [Fact]
    public void ThemeDeclaresEverySpacingRadiusFontAndControlSizeToken()
    {
        RunOnThemeThread((light, _) =>
        {
            AssertTokensOfType<double>(light, SpacingTokens, "间距");
            AssertTokensOfType<double>(light, FontSizeTokens, "字号");
            AssertTokensOfType<double>(light, ControlSizeTokens, "控件尺寸");

            // 圆角必须是 CornerRadius 而不是 double：Border.CornerRadius 需要 CornerRadius 值，
            // 把一个 sys:Double 令牌绑给它会在 Arrange 阶段抛 InvalidCastException。
            AssertTokensOfType<CornerRadius>(light, RadiusTokens, "圆角");
        });
    }

    /// <summary>
    /// 令牌类型必须匹配其消费属性，否则运行时才炸。
    ///
    /// 这条测试来自一次真实回归：圆角令牌最初声明为 <c>sys:Double</c>，契约测试因为
    /// 只断言「键存在且是 double」而全部通过，但 15 个 WPF 运行时测试在
    /// <c>Border.ArrangeOverride</c> 抛 <c>InvalidCastException</c>。教训是：
    /// 「键存在 + 类型是某个值」不足以证明它能被目标属性消费，必须断言**具体的 CLR 类型**。
    /// </summary>
    [Fact]
    public void RadiusTokenTypeMatchesCornerRadiusProperty()
    {
        RunOnThemeThread((light, _) =>
        {
            foreach (string key in RadiusTokens)
            {
                Assert.True(
                    light.Contains(key),
                    $"圆角令牌 '{key}' 未声明。");
                Assert.True(
                    light[key] is CornerRadius,
                    $"圆角令牌 '{key}' 必须是 CornerRadius（供 Border/Button 等的 CornerRadius 属性使用），"
                        + $"实际为 {light[key]?.GetType().Name ?? "null"}。");
            }
        });
    }

    /// <summary>
    /// 令牌必须能被真正消费它的控件接受。
    ///
    /// 与 <see cref="RadiusTokenTypeMatchesCornerRadiusProperty"/> 互补：那条断言类型，
    /// 这条把令牌实际赋给一个真实 <see cref="System.Windows.Controls.Border"/> 并强制布局，
    /// 覆盖「类型对但赋值路径仍然失败」的情况（例如资源查找失败、转换器不适用）。
    /// </summary>
    [Fact]
    public void RadiusTokenCanBeAppliedToARealBorder()
    {
        WpfRuntimeHost.Run(() =>
        {
            foreach (string key in RadiusTokens)
            {
                var border = new System.Windows.Controls.Border
                {
                    Width = 40d,
                    Height = 40d,
                    CornerRadius = (CornerRadius)Application.Current.FindResource(key),
                };

                var window = new Window
                {
                    Content = border,
                    Width = 60d,
                    Height = 60d,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                };

                try
                {
                    window.Show();
                    // 强制布局：CornerRadius 的类型错误只在 Arrange 阶段暴露。
                    window.UpdateLayout();
                    Assert.True(border.ActualWidth > 0d);
                }
                finally
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void RadiusTokensStayWithinTheDocumentedEightPixelCeiling()
    {
        RunOnThemeThread((light, _) =>
        {
            foreach (string key in RadiusTokens)
            {
                Assert.True(
                    light[key] is CornerRadius,
                    $"圆角令牌 '{key}' 未声明或不是 CornerRadius。");

                var radius = (CornerRadius)light[key]!;
                double maximum = Math.Max(
                    Math.Max(radius.TopLeft, radius.TopRight),
                    Math.Max(radius.BottomLeft, radius.BottomRight));

                Assert.True(
                    maximum <= 8d,
                    $"圆角令牌 '{key}' = {maximum}，超出 UI_DESIGN §2.2 的 8px 上限。");
            }
        });
    }

    [Fact]
    public void ThemeDeclaresMotionTokens()
    {
        RunOnThemeThread((light, _) =>
        {
            AssertTokensOfType<Duration>(light, MotionDurationTokens, "动效时长");
            AssertAllTokensExist(light, EasingTokens, "缓动");
        });
    }

    [Fact]
    public void DarkThemeOverridesEverySemanticColorToken()
    {
        RunOnThemeThread((light, dark) =>
        {
            foreach (string key in ColorTokens)
            {
                Assert.True(
                    light.Contains(key),
                    $"颜色令牌 '{key}' 未在 Theme.xaml（浅色基准）中声明。");
                Assert.True(
                    dark.Contains(key),
                    $"颜色令牌 '{key}' 未在 DarkTheme.xaml 中覆写；深色主题下会沿用浅色值。");
            }
        });
    }

    [Fact]
    public void ThemeDeclaresEveryDynamicResourceKeyItReferences()
    {
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");
        string darkTheme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "DarkTheme.xaml");

        // 由 DesktopAppearanceController 在桌面组件窗口内注入的键：
        // 它们只存在于桌面窗口的 window.Resources，不属于全局主题契约。
        // 注意：UiTextEffect 已不在此列表——Theme.xaml 不再引用它。
        // 文字描边是桌面组件特性，由 DesktopAppearanceController 直接写入
        // 窗口资源并由桌面窗口自己的样式消费，全局主题不应声明该键。
        string[] desktopWindowScoped =
        [
            "DesktopSurfaceBrush",
            "DesktopPrimaryTextBrush",
            "DesktopSecondaryTextBrush",
            "DesktopDateTextBrush",
            "DesktopLunarTextBrush",
            "DesktopWeekendTextBrush",
            "DesktopHolidayTextBrush",
            "DesktopDayOffBackgroundBrush",
            "DesktopDayOffTextBrush",
            "DesktopWorkdayBackgroundBrush",
            "DesktopWorkdayTextBrush",
            "DesktopTodayBackgroundBrush",
            "DesktopTodayTextBrush",
            "DesktopSeparatorBrush",
            "DesktopContentPadding",
            "DesktopOtherMonthOpacity",
        ];

        HashSet<string> declared = [.. ExtractDeclaredKeys(theme), .. ExtractDeclaredKeys(darkTheme)];
        string[] missing =
        [
            .. ExtractReferencedDynamicResourceKeys(theme)
                .Where(key => !declared.Contains(key))
                .Where(key => !desktopWindowScoped.Contains(key, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal),
        ];

        Assert.True(
            missing.Length == 0,
            $"Theme.xaml 引用了未声明的 DynamicResource 键：{string.Join(", ", missing)}。"
                + "运行时这些引用会静默解析为空值，界面回退到系统默认样式。");
    }

    [Fact]
    public void ThemeDeclaresEachKeyExactlyOnce()
    {
        RunOnThemeThread((light, dark) =>
        {
            foreach (string path in new[] { ThemeRelativePath, DarkThemeRelativePath })
            {
                string content = ReadWorkspaceFile(path.Split('/'));
                string[] duplicates =
                [
                    .. ExtractDeclaredKeys(content)
                        .GroupBy(key => key, StringComparer.Ordinal)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key)
                        .OrderBy(key => key, StringComparer.Ordinal),
                ];

                Assert.True(
                    duplicates.Length == 0,
                    $"{path} 中存在重复声明的资源键：{string.Join(", ", duplicates)}。"
                        + "后声明的键会覆盖先声明的键。");
            }

            // 两个字典在运行时同时合并，因此不能声明同名键（深色靠覆写同一键生效）。
            Assert.NotNull(light);
            Assert.NotNull(dark);
        });
    }

    /// <summary>
    /// 细粒度结构尺寸的豁免清单（P60-02）。
    ///
    /// 豁免的唯一理由是「**没有数值相等的令牌可用**」，而不是「值很小」或「看起来不重要」。
    /// 归并到最近的令牌会改变控件外观，因此这些值刻意保留为字面量；显式登记的目的是让
    /// 「豁免」成为一个需要写下来的决定，而不是漏改的借口：新增任何豁免值都必须同步改这里，
    /// 否则 <see cref="ThemeRadiusLiteralsAreTokenized"/> 会失败。
    ///
    /// 反例（曾考虑豁免但实际应令牌化）：Slider thumb 的 `CornerRadius="8"` 虽然也是
    /// 「小控件上的圆角」，但 8 与 `RadiusLg` 数值相等，替换后渲染值不变，故已令牌化。
    /// </summary>
    private static readonly Dictionary<string, string> StructuralRadiusExemptions = new(StringComparer.Ordinal)
    {
        ["3"] = "CheckBox 勾选框（16px 方框配 3px 圆角，无同值令牌）",
        ["2"] = "ProgressBar / Slider 轨道 / 导航选中条（2px 细条结构尺寸，无同值令牌）",
        ["5"] = "ScrollBar thumb（10px 宽 thumb 的圆角，无同值令牌）",
    };

    [Fact]
    public void ThemeRadiusLiteralsAreTokenized()
    {
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");

        // 设计与令牌同值的圆角必须改用令牌引用（替换后渲染值完全相同）。
        foreach (string value in new[] { "4", "6", "8" })
        {
            Assert.True(
                !theme.Contains($"CornerRadius=\"{value}\"", StringComparison.Ordinal),
                $"Theme.xaml 仍有 CornerRadius=\"{value}\" 字面量；"
                    + $"应改用 {RadiusTokenFor(value)} 令牌（数值相同，观感不变）。");
        }

        // 豁免的结构尺寸必须与登记表逐项一致，防止豁免清单被悄悄扩张或过期。
        string[] presentExemptions =
        [
            .. StructuralRadiusExemptions.Keys
                .Where(value => theme.Contains($"CornerRadius=\"{value}\"", StringComparison.Ordinal))
                .OrderBy(value => value, StringComparer.Ordinal),
        ];
        string[] expectedExemptions =
        [
            .. StructuralRadiusExemptions.Keys.OrderBy(value => value, StringComparer.Ordinal),
        ];

        Assert.Equal(expectedExemptions, presentExemptions);
    }

    [Fact]
    public void ThemeStyleFontSizesAreTokenized()
    {
        string theme = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Themes", "Theme.xaml");

        // 样式级字号必须走令牌；模板内的 <Setter Property="FontSize"> 仅允许令牌引用。
        foreach (string value in new[] { "12", "16", "20" })
        {
            Assert.True(
                !theme.Contains($"Property=\"FontSize\" Value=\"{value}\"", StringComparison.Ordinal),
                $"Theme.xaml 仍有 FontSize=\"{value}\" 字面量；应改用字号令牌。");
        }
    }

    private static string RadiusTokenFor(string value) => value switch
    {
        "4" => "RadiusSm",
        "6" => "RadiusMd",
        "8" => "RadiusLg",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "该值不在可令牌化范围内。"),
    };

    private static void AssertTokensOfType<T>(ResourceDictionary dictionary, string[] keys, string category)
    {
        foreach (string key in keys)
        {
            Assert.True(
                dictionary.Contains(key),
                $"{category}令牌 '{key}' 未在 Theme.xaml 中声明。");
            Assert.True(
                dictionary[key] is T,
                $"{category}令牌 '{key}' 声明类型应为 {typeof(T).Name}，实际为 "
                    + $"{dictionary[key]?.GetType().Name ?? "null"}。");
        }
    }

    private static void AssertAllTokensExist(ResourceDictionary dictionary, string[] keys, string category)
    {
        foreach (string key in keys)
        {
            Assert.True(
                dictionary.Contains(key),
                $"{category}令牌 '{key}' 未在 Theme.xaml 中声明。");
        }
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

    /// <summary>提取 <c>x:Key="..."</c> 声明的键名。</summary>
    private static IEnumerable<string> ExtractDeclaredKeys(string xaml)
    {
        foreach (System.Text.RegularExpressions.Match match in
            System.Text.RegularExpressions.Regex.Matches(xaml, "x:Key=\"([^\"]+)\""))
        {
            yield return match.Groups[1].Value;
        }
    }

    /// <summary>提取 <c>{DynamicResource X}</c> 形式引用的键名。</summary>
    private static IEnumerable<string> ExtractReferencedDynamicResourceKeys(string xaml)
    {
        foreach (System.Text.RegularExpressions.Match match in
            System.Text.RegularExpressions.Regex.Matches(xaml, @"\{DynamicResource\s+([^\}]+)\}"))
        {
            yield return match.Groups[1].Value.Trim();
        }
    }

    private static string ReadWorkspaceFile(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CcCalendar.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. segments]));
    }
}
