using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CcCalendar.Desktop.Controls;

/// <summary>
/// 加载环（P61-03）。用于「正在加载 / 正在处理」的场景，替代原先只能写文字的做法。
///
/// <see cref="IsActive"/> 为 true 时上弧持续旋转；为 false 时停表并隐藏，
/// 避免离屏控件继续消耗渲染时间。
/// </summary>
public partial class LoadingRing : UserControl
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(LoadingRing),
            new PropertyMetadata(false, OnIsActiveChanged));

    /// <summary>旋转一周的时长令牌名。</summary>
    public const string SpinDurationToken = "SpinDuration";

    /// <summary>缺少令牌时的回退旋转周期（毫秒）。</summary>
    public const double FallbackSpinMilliseconds = 900d;

    private DoubleAnimation? spin;

    public LoadingRing()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyState();
        Unloaded += (_, _) => StopSpin();
    }

    /// <summary>是否正在旋转。</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>当前旋转角度，供测试断言动画确实在推进。</summary>
    public double CurrentAngle => ArcRotation.Angle;

    private static void OnIsActiveChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        => ((LoadingRing)element).ApplyState();

    private void ApplyState()
    {
        if (IsActive)
        {
            StartSpin();
        }
        else
        {
            StopSpin();
        }
    }

    private void StartSpin()
    {
        // 缩放：让弧的半径跟随 SpinnerSize，而描边厚度保持视觉一致。
        double size = TryFindResource("SpinnerSize") is double configured ? configured : 16d;
        double scale = size / 16d;
        ArcScale.ScaleX = scale;
        ArcScale.ScaleY = scale;

        Visibility = Visibility.Visible;

        // 直接把动画时钟挂到 RotateTransform 上（而不是 BeginStoryboard + 名称查找）：
        // TransformGroup 内的目标用 Storyboard.Target 解析不稳定，直接 BeginAnimation
        // 只依赖对象引用，行为确定。
        spin ??= BuildAnimation();
        ArcRotation.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    private void StopSpin()
    {
        ArcRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        ArcRotation.Angle = 0d;
        Visibility = Visibility.Collapsed;
    }

    private DoubleAnimation BuildAnimation()
    {
        Duration duration = TryFindResource(SpinDurationToken) is Duration configured
            ? configured
            : new Duration(TimeSpan.FromMilliseconds(FallbackSpinMilliseconds));

        return new DoubleAnimation
        {
            From = 0d,
            To = 360d,
            Duration = duration,
            RepeatBehavior = RepeatBehavior.Forever,
        };
    }
}
