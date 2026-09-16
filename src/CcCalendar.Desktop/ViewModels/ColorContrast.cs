using System.Windows.Media;

namespace CcCalendar.Desktop.ViewModels;

/// <summary>
/// WCAG 2.1 相对亮度与对比度计算（P60-04）。
///
/// 放在产品代码而不是测试代码里，是为了让对比度下限有一个可复用的定义：
/// 测试用它断言令牌，将来若增加主题或配色也能用同一套公式校验，
/// 而不是各自抄一遍。
/// </summary>
public static class ColorContrast
{
    /// <summary>正文文字的可读下限（WCAG AA，普通字号）。</summary>
    public const double TextMinimum = 4.5d;

    /// <summary>图形与界面控件的可读下限（WCAG AA，非文字）。</summary>
    public const double NonTextMinimum = 3d;

    /// <summary>WCAG 2.1 相对亮度，取值 0–1。</summary>
    public static double RelativeLuminance(Color color)
        => (0.2126d * Linearize(color.R))
            + (0.7152d * Linearize(color.G))
            + (0.0722d * Linearize(color.B));

    /// <summary>两色的对比度，取值 1–21。</summary>
    public static double Ratio(Color first, Color second)
    {
        double a = RelativeLuminance(first);
        double b = RelativeLuminance(second);
        double lighter = Math.Max(a, b);
        double darker = Math.Min(a, b);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double Linearize(byte channel)
    {
        double value = channel / 255d;
        return value <= 0.03928d
            ? value / 12.92d
            : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    }
}
