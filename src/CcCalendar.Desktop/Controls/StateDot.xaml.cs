using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CcCalendar.Desktop.Controls;

/// <summary>
/// 状态点（P61-03）。一个 8px 的圆点，可选呼吸光晕，用于表达「进行中 / 在线 / 空闲」
/// 这类持续状态——比静态色块更能说明"它还在动"。
///
/// 颜色由 <see cref="StateBrushKey"/> 指定**主题令牌名**（<c>SuccessBrush</c>、
/// <c>WarningBrush</c>、<c>DangerBrush</c>、<c>AccentBrush</c>…），而不是直接给一个 Brush：
/// 这样深浅主题切换时颜色会跟着解析到新值。
///
/// 画刷是每个实例独有的（构造时创建），因此改色与动画都不会串到其它状态点。
/// </summary>
public partial class StateDot : UserControl
{
    /// <summary>呼吸周期令牌名。</summary>
    public const string PulseDurationToken = "PulseDuration";

    /// <summary>缺少令牌时的回退呼吸周期（毫秒）。</summary>
    public const double FallbackPulseMilliseconds = 1200d;

    /// <summary>光晕最大放大倍数。</summary>
    public const double HaloMaxScale = 2.4d;

    /// <summary>解析不到令牌时使用的兜底色（主题的 SuccessBrush 浅色值）。</summary>
    private static readonly Color FallbackColor = Color.FromRgb(0x2E, 0x7D, 0x4F);

    public static readonly DependencyProperty IsPulsingProperty =
        DependencyProperty.Register(
            nameof(IsPulsing),
            typeof(bool),
            typeof(StateDot),
            new PropertyMetadata(false, OnIsPulsingChanged));

    public static readonly DependencyProperty StateBrushKeyProperty =
        DependencyProperty.Register(
            nameof(StateBrushKey),
            typeof(string),
            typeof(StateDot),
            new PropertyMetadata(null, OnStateBrushKeyChanged));

    private readonly SolidColorBrush dotBrush;

    private Storyboard? pulse;

    public StateDot()
    {
        // 每个实例一个画刷，避免共享 Freezable 造成跨实例串扰。
        dotBrush = new SolidColorBrush(FallbackColor);
        InitializeComponent();
        Halo.Fill = dotBrush;
        Core.Fill = dotBrush;

        Loaded += (_, _) => ApplyState();
        Unloaded += (_, _) => StopPulse();
    }

    /// <summary>是否显示呼吸光晕。</summary>
    public bool IsPulsing
    {
        get => (bool)GetValue(IsPulsingProperty);
        set => SetValue(IsPulsingProperty, value);
    }

    /// <summary>状态色令牌名（例如 <c>SuccessBrush</c>）；空值沿用兜底色。</summary>
    public string? StateBrushKey
    {
        get => (string?)GetValue(StateBrushKeyProperty);
        set => SetValue(StateBrushKeyProperty, value);
    }

    /// <summary>当前光晕不透明度，供测试断言呼吸确实在推进。</summary>
    public double CurrentHaloOpacity => Halo.Opacity;

    /// <summary>当前状态色，供测试断言令牌解析结果。</summary>
    public Color CurrentColor => dotBrush.Color;

    private static void OnIsPulsingChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        => ((StateDot)element).ApplyState();

    private static void OnStateBrushKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        => ((StateDot)element).ApplyBrush();

    /// <summary>
    /// 解析状态色。主题令牌是 <c>SolidColorBrush</c>；直接取它的 Color 赋给本实例画刷，
    /// 因此改令牌（或切主题）后重新解析即可跟随。
    /// </summary>
    public void ApplyBrush()
    {
        Color color = StateBrushKey is not null
            && TryFindResource(StateBrushKey) is SolidColorBrush resolved
                ? resolved.Color
                : FallbackColor;

        dotBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        dotBrush.Color = color;
    }

    private void ApplyState()
    {
        ApplyBrush();

        if (IsPulsing)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }
    }

    private void StartPulse()
    {
        pulse ??= BuildStoryboard();
        BeginStoryboard(pulse);
    }

    private void StopPulse()
    {
        if (pulse is not null)
        {
            pulse.Stop(this);
        }

        HaloScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        HaloScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Halo.BeginAnimation(UIElement.OpacityProperty, null);
        HaloScale.ScaleX = 1d;
        HaloScale.ScaleY = 1d;
        Halo.Opacity = 0d;
    }

    private Storyboard BuildStoryboard()
    {
        Duration duration = TryFindResource(PulseDurationToken) is Duration configured
            ? configured
            : new Duration(TimeSpan.FromMilliseconds(FallbackPulseMilliseconds));

        var scaleX = new DoubleAnimation
        {
            From = 1d,
            To = HaloMaxScale,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        var scaleY = new DoubleAnimation
        {
            From = 1d,
            To = HaloMaxScale,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        var opacity = new DoubleAnimation
        {
            From = 0.55d,
            To = 0d,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };

        var storyboard = new Storyboard();
        Add(storyboard, scaleX, HaloScale, ScaleTransform.ScaleXProperty);
        Add(storyboard, scaleY, HaloScale, ScaleTransform.ScaleYProperty);
        Add(storyboard, opacity, Halo, UIElement.OpacityProperty);
        return storyboard;
    }

    private static void Add(Storyboard storyboard, DoubleAnimation animation, DependencyObject target, DependencyProperty property)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        storyboard.Children.Add(animation);
    }
}
