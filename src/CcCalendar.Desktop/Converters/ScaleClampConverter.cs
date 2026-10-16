using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CcCalendar.Desktop.Converters;

/// <summary>
/// P72：聊天宽度轴转换器（移植 harness ConversationRoot 的
/// clamp(680px, column×64%, 920px) 与 MessageItem 的 0.702 气泡上限）。
/// 参数格式 "scale[;min;max[;offset]]"：源×scale，可选夹到 [min,max]，
/// 再可选加 offset。源必须是独立的布局输出（ListBox/Grid 的 ActualWidth
/// 或兄弟元素的 MaxWidth）——绑模板自身 ActualWidth 会形成循环依赖，
/// P72 曾因此把用户气泡卡死在 0 宽（提问不显示），勿重蹈。
/// </summary>
public sealed class ScaleClampConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double source || double.IsNaN(source))
        {
            return DependencyProperty.UnsetValue;
        }

        string[] parts = parameter?.ToString()?.Split(';') ?? [];
        if (parts.Length == 0
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
        {
            return DependencyProperty.UnsetValue;
        }

        double result = source * scale;
        if (parts.Length > 1
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double min))
        {
            result = Math.Max(result, min);
        }

        if (parts.Length > 2
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
        {
            result = Math.Min(result, max);
        }

        if (parts.Length > 3
            && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double offset))
        {
            result += offset;
        }

        return result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
