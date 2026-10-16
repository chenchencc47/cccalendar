using System.Windows;
using System.Windows.Media.Animation;

namespace CcCalendar.Desktop.Views;

/// <summary>
/// P73：ColumnDefinition.Width（GridLength）的补间动画。WPF 没有内置
/// GridLength 动画，标准做法是自定义 AnimationTimeline。注意 EasingFunction
/// 与内置动画不同——时钟不会自动套用，必须在 GetCurrentValue 里手动应用。
/// </summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(
            nameof(From), typeof(GridLength), typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(
            nameof(To), typeof(GridLength), typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction { get; init; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(
        object defaultOriginValue,
        object defaultDestinationValue,
        AnimationClock animationClock)
    {
        double progress = animationClock.CurrentProgress ?? 0;
        if (EasingFunction is not null)
        {
            progress = EasingFunction.Ease(progress);
        }

        return new GridLength(
            From.Value + ((To.Value - From.Value) * progress),
            GridUnitType.Pixel);
    }
}
