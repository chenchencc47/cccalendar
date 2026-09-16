using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CcCalendar.Desktop.Controls;

namespace CcCalendar.Desktop.Tests.Views;

/// <summary>
/// P61-03 微组件契约：加载环、状态点、连接状态指示器。
///
/// 主题此前没有这类控件（0 个 keyframes / 0 个 Storyboard），"正在加载"只能用文字代替。
/// 这组测试既断言令牌与状态映射，也断言**动画在运行时真的推进**——只断言属性存在
/// 无法说明控件会动。
/// </summary>
public sealed class MicroComponentTests
{
    private const string ThemeSource = "pack://application:,,,/cccalendar;component/Themes/Theme.xaml";

    private static readonly string[] SizeTokens =
    [
        "SpinnerSize",
        "SpinnerStrokeThickness",
        "StateDotSize",
        "StateDotActiveSize",
    ];

    private static readonly string[] DurationTokens =
    [
        LoadingRing.SpinDurationToken,
        StateDot.PulseDurationToken,
    ];

    [Fact]
    public void MicroComponentTokensExistWithCorrectTypes()
    {
        WpfRuntimeHost.Run(() =>
        {
            var theme = new ResourceDictionary { Source = new Uri(ThemeSource) };

            foreach (string key in SizeTokens)
            {
                Assert.True(theme.Contains(key), $"缺少尺寸令牌 {key}。");
                Assert.IsType<double>(theme[key]);
            }

            foreach (string key in DurationTokens)
            {
                Assert.True(theme.Contains(key), $"缺少时长令牌 {key}。");
                Assert.IsType<Duration>(theme[key]);
            }
        });
    }

    [Fact]
    public void LoadingRingHidesWhenIdleAndRotatesWhenActive()
    {
        WpfRuntimeHost.Run(() =>
        {
            var ring = new LoadingRing();
            var window = new Window { Content = ring, Width = 80, Height = 80, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(window);

                // 未激活：不可见且不转。
                Assert.Equal(Visibility.Collapsed, ring.Visibility);
                Assert.Equal(0d, ring.CurrentAngle);

                ring.IsActive = true;
                Pump(window);

                Assert.Equal(Visibility.Visible, ring.Visibility);

                // 等时钟推进，确认角度确实在变（而不是只设了属性）。
                Assert.True(
                    WaitFor(() => ring.CurrentAngle > 0d, window),
                    $"加载环激活后角度仍为 {ring.CurrentAngle}，旋转动画没有推进。");

                ring.IsActive = false;
                Pump(window);
                Assert.Equal(Visibility.Collapsed, ring.Visibility);
                Assert.Equal(0d, ring.CurrentAngle);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void StateDotPulsesOnlyWhenRequested()
    {
        WpfRuntimeHost.Run(() =>
        {
            var dot = new StateDot();
            var window = new Window { Content = dot, Width = 60, Height = 60, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(window);

                Assert.Equal(0d, dot.CurrentHaloOpacity);

                dot.IsPulsing = true;
                Pump(window);

                Assert.True(
                    WaitFor(() => dot.CurrentHaloOpacity > 0d, window),
                    $"状态点开启呼吸后光晕不透明度仍为 {dot.CurrentHaloOpacity}。");

                dot.IsPulsing = false;
                Pump(window);
                Assert.Equal(0d, dot.CurrentHaloOpacity);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>状态点必须解析主题令牌取色，而不是写死颜色。</summary>
    [Fact]
    public void StateDotResolvesThemeTokensForItsColour()
    {
        WpfRuntimeHost.Run(() =>
        {
            var dot = new StateDot();
            var window = new Window { Content = dot, Width = 60, Height = 60, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(window);

                dot.StateBrushKey = "SuccessBrush";
                dot.ApplyBrush();
                Color success = dot.CurrentColor;

                dot.StateBrushKey = "DangerBrush";
                dot.ApplyBrush();
                Color danger = dot.CurrentColor;

                Assert.NotEqual(success, danger);

                var theme = new ResourceDictionary { Source = new Uri(ThemeSource) };
                Assert.Equal(
                    Assert.IsType<SolidColorBrush>(theme["SuccessBrush"]).Color,
                    success);
                Assert.Equal(
                    Assert.IsType<SolidColorBrush>(theme["DangerBrush"]).Color,
                    danger);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>连接指示器的三态映射：连接中 &gt; 已连接 &gt; 未连接。</summary>
    [Fact]
    public void ConnectionIndicatorMapsBusyThenConnectedThenDisconnected()
    {
        WpfRuntimeHost.Run(() =>
        {
            var indicator = new ConnectionIndicator();
            var window = new Window { Content = indicator, Width = 200, Height = 60, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump(window);

                Assert.Equal(ConnectionIndicator.DisconnectedBrushKey, indicator.CurrentBrushKey);

                indicator.IsConnected = true;
                Pump(window);
                Assert.Equal(ConnectionIndicator.ConnectedBrushKey, indicator.CurrentBrushKey);

                // Busy 优先级高于 Connected。
                indicator.IsBusy = true;
                Pump(window);
                Assert.Equal(ConnectionIndicator.BusyBrushKey, indicator.CurrentBrushKey);

                indicator.IsBusy = false;
                Pump(window);
                Assert.Equal(ConnectionIndicator.ConnectedBrushKey, indicator.CurrentBrushKey);

                indicator.IsConnected = false;
                Pump(window);
                Assert.Equal(ConnectionIndicator.DisconnectedBrushKey, indicator.CurrentBrushKey);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// P61-03：连接指示器必须真的接在设置页的团队连接区，而不是只做了控件没人用。
    /// </summary>
    [Fact]
    public void SettingsTeamConnectionUsesTheConnectionIndicator()
    {
        string settings = ReadWorkspaceFile("src", "CcCalendar.Desktop", "Views", "SettingsView.xaml");

        Assert.Contains("xmlns:controls=", settings, StringComparison.Ordinal);
        Assert.Contains("<controls:ConnectionIndicator", settings, StringComparison.Ordinal);
        Assert.Contains("IsConnected=\"{Binding IsAuthenticated", settings, StringComparison.Ordinal);
        Assert.Contains("IsBusy=\"{Binding IsBusy", settings, StringComparison.Ordinal);

        // 原先重复的「登录状态：True/False」绑定应已被状态点取代。
        // 注意不能用 StringFormat= 做宽泛断言：设置页另有 4 处合法的数值格式化。
        Assert.DoesNotContain("StringFormat=登录状态", settings, StringComparison.Ordinal);
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
    private static bool WaitFor(Func<bool> condition, Window window, int milliseconds = 2000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(25);
            Pump(window);
        }

        return condition();
    }

    private static void Pump(Window window)
    {
        window.Dispatcher.Invoke(DispatcherPriority.Background, () => { });
        window.Dispatcher.Invoke(DispatcherPriority.Render, () => { });
    }
}
