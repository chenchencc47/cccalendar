using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CcCalendar.Desktop.Interactions;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// P61-01 交互动效契约。
///
/// 项目在此之前**没有任何声明式动画**（无 Storyboard、无 VisualStateManager），
/// 悬停/按下都是瞬间跳变。这组测试把动效约定钉住：
/// 令牌存在且类型正确、叠加色确实带 alpha、以及模板里真的挂了过渡动画并能生效。
/// </summary>
public sealed class MotionContractTests
{
    private const string ThemeSource =
        "pack://application:,,,/cccalendar;component/Themes/Theme.xaml";
    private const string DarkThemeSource =
        "pack://application:,,,/cccalendar;component/Themes/DarkTheme.xaml";
    private const string ThemeRelativePath = "src/CcCalendar.Desktop/Themes/Theme.xaml";

    [Fact]
    public void MotionTokensExistWithCorrectTypes()
    {
        WpfRuntimeHost.Run(() =>
        {
            var theme = new ResourceDictionary { Source = new Uri(ThemeSource) };

            Assert.True(theme.Contains(SmoothTint.MotionDurationToken), "缺少过渡时长令牌 MotionFast。");
            Assert.IsType<Duration>(theme[SmoothTint.MotionDurationToken]);

            Assert.True(theme.Contains(SmoothTint.MotionEasingToken), "缺少缓动令牌 EasingStandard。");
            Assert.IsAssignableFrom<System.Windows.Media.Animation.IEasingFunction>(
                theme[SmoothTint.MotionEasingToken]);
        });
    }

    /// <summary>
    /// 叠加色必须带 alpha：不透明的叠加色会盖住底色，悬停就会把按钮「刷成另一块色」，
    /// 也失去了「一组叠加色适配多种底色」的意义。
    /// </summary>
    [Fact]
    public void OverlayBrushesAreTranslucentInBothThemes()
    {
        WpfRuntimeHost.Run(() =>
        {
            foreach (string source in new[] { ThemeSource, DarkThemeSource })
            {
                var theme = new ResourceDictionary { Source = new Uri(source) };
                foreach (string key in SmoothTint.OverlayBrushKeys)
                {
                    Assert.True(theme.Contains(key), $"{source} 缺少叠加色令牌 {key}。");
                    var brush = Assert.IsType<SolidColorBrush>(theme[key]);

                    Assert.True(
                        brush.Color.A is > 0 and < 255,
                        $"{key} 的 alpha={brush.Color.A}，应为半透明（0 < A < 255）；"
                            + "不透明叠加会盖住底色。");
                }
            }
        });
    }

    /// <summary>
    /// 深色主题必须覆写**普通底色**上的叠加色，否则深色下悬停会越悬越暗。
    ///
    /// 「强调色底上的白色叠加」两份主题刻意保持一致：强调色本身就是蓝色，
    /// 白色叠加在深浅两个主题下语义相同，不做主题区分。这是设计决定，不是漏改。
    /// </summary>
    [Fact]
    public void DarkThemeOverridesSurfaceOverlayBrushes()
    {
        WpfRuntimeHost.Run(() =>
        {
            var light = new ResourceDictionary { Source = new Uri(ThemeSource) };
            var dark = new ResourceDictionary { Source = new Uri(DarkThemeSource) };

            string[] surfaceOverlays =
            [
                SmoothTint.HoverBrushKey,
                SmoothTint.PressedBrushKey,
            ];

            foreach (string key in surfaceOverlays)
            {
                var lightColor = Assert.IsType<SolidColorBrush>(light[key]).Color;
                var darkColor = Assert.IsType<SolidColorBrush>(dark[key]).Color;
                Assert.True(lightColor != darkColor, $"叠加色 {key} 在深色主题下未覆写。");
            }

            // 白色叠加必须真的是白色，且两主题一致。
            foreach (string key in new[] { SmoothTint.HoverOnAccentBrushKey, SmoothTint.PressedOnAccentBrushKey })
            {
                var lightColor = Assert.IsType<SolidColorBrush>(light[key]).Color;
                var darkColor = Assert.IsType<SolidColorBrush>(dark[key]).Color;

                Assert.True(
                    lightColor.R == 0xFF && lightColor.G == 0xFF && lightColor.B == 0xFF,
                    $"{key} 应为白色叠加（强调色底），实际 {lightColor}。");
                Assert.Equal(lightColor, darkColor);
            }
        });
    }

    /// <summary>
    /// 模板必须真的挂了过渡动画。WPF 的模板触发器由真实鼠标/键盘输入驱动，
    /// 合成 <c>MouseEnterEvent</c> 不会触发 <c>IsMouseOver</c>，因此这里断言模板标记，
    /// 而「动画在运行时确实会推进」由 <see cref="OverlayOpacityAnimationActuallyProgresses"/> 覆盖。
    /// </summary>
    [Fact]
    public void ButtonTemplatesWireHoverTransitions()
    {
        string theme = ReadWorkspaceFile(ThemeRelativePath.Split('/'));

        Assert.Contains("x:Name=\"ButtonTint\"", theme, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PrimaryButtonTint\"", theme, StringComparison.Ordinal);

        // 悬停进入/退出都应挂 Storyboard（而不是瞬间 Setter 换色）。
        Assert.Contains("Trigger.EnterActions", theme, StringComparison.Ordinal);
        Assert.Contains("Trigger.ExitActions", theme, StringComparison.Ordinal);
        Assert.Contains("Storyboard.TargetProperty=\"Opacity\"", theme, StringComparison.Ordinal);
        Assert.Contains("Duration=\"0:0:0.12\"", theme, StringComparison.Ordinal);
        Assert.Contains("CubicEase EasingMode=\"EaseInOut\"", theme, StringComparison.Ordinal);

        // 旧的「瞬间换不透明底色」写法不应再出现在按钮模板里。
        Assert.DoesNotContain(
            "TargetName=\"ButtonBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            theme,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 用与模板相同的时长/缓动，验证「在叠加 Border 上动 Opacity」这条路在运行时真的会推进。
    ///
    /// 这补上了模板触发器无法被合成事件驱动的缺口：如果 WPF 在这个场景下不推进动画，
    /// 这里会一并失败，而不是等到用户悬停时才发现没效果。
    /// </summary>
    [Fact]
    public void OverlayOpacityAnimationActuallyProgresses()
    {
        WpfRuntimeHost.Run(() =>
        {
            var tint = new Border { Width = 60, Height = 24, Opacity = 0 };
            var window = new Window { Content = tint, Width = 120, Height = 60, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(window);

                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = SmoothTint.ActiveOpacity,
                    Duration = new Duration(TimeSpan.FromMilliseconds(120)),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut,
                    },
                };

                tint.BeginAnimation(UIElement.OpacityProperty, animation);
                Pump(window);

                // 等时钟真正推进过动画时长，再看终值。WPF 的动画时钟独立于 Dispatcher，
                // 因此必须真等待，不能只 Pump 一次。
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (tint.Opacity < SmoothTint.ActiveOpacity && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(25);
                    Pump(window);
                }

                Assert.True(
                    tint.Opacity >= SmoothTint.ActiveOpacity,
                    $"叠加层 Opacity 动画未推进到目标值，实际停在 {tint.Opacity}。");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P61-05：所有可交互控件模板都必须通过叠加层做过渡，而不是瞬间换不透明底色。
    ///
    /// 断言两件事：每个控件模板里都有命名叠加层；且模板中不再出现
    /// 「直接给背景设 HoverBrush」的瞬间换色写法（Slider thumb 除外——它的悬停
    /// 语义就是填充强调色，不是叠加）。
    /// </summary>
    [Fact]
    public void EveryInteractiveTemplateUsesAnAnimatedTintOverlay()
    {
        string theme = ReadWorkspaceFile(ThemeRelativePath.Split('/'));

        string[] expectedTints =
        [
            "ButtonTint",
            "PrimaryButtonTint",
            "ListItemTint",
            "NavigationTint",
            "ComboItemTint",
            "TabTint",
            "ToggleTint",
            "MenuItemTint",
            "HeaderTint",
        ];

        foreach (string tint in expectedTints)
        {
            Assert.Contains($"x:Name=\"{tint}\"", theme, StringComparison.Ordinal);
        }

        // 瞬间换色写法应已清除（Slider thumb 的 AccentHoverBrush 是刻意的填充语义）。
        foreach (string instant in new[]
        {
            "TargetName=\"ButtonBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            "TargetName=\"ListItemBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            "TargetName=\"ItemBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            "TargetName=\"ComboItemBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            "TargetName=\"TabBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            "TargetName=\"ToggleBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            "TargetName=\"MenuItemBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
            "TargetName=\"HeaderBorder\" Property=\"Background\" Value=\"{DynamicResource HoverBrush}\"",
        })
        {
            Assert.DoesNotContain(instant, theme, StringComparison.Ordinal);
        }
    }

    private static Border FindTint(DependencyObject root, string name)
    {
        if (root is FrameworkElement element && element.Name == name && element is Border border)
        {
            return border;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            try
            {
                return FindTint(child, name);
            }
            catch (InvalidOperationException)
            {
                // 该子树里没有，继续找下一个兄弟。
            }
        }

        throw new InvalidOperationException($"未在模板中找到名为 {name} 的 Border。");
    }

    private static void Pump(Window window)
    {
        window.Dispatcher.Invoke(DispatcherPriority.Background, () => { });
        window.Dispatcher.Invoke(DispatcherPriority.Render, () => { });
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
