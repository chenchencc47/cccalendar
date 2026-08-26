using System.Globalization;

namespace CcCalendar.Desktop.Desktop;

public static class DesktopSizeEditorState
{
    public static DesktopWindowSize Parse(string widthText, string heightText)
    {
        if (!double.TryParse(widthText, NumberStyles.Number, CultureInfo.CurrentCulture, out double width)
            || !double.TryParse(heightText, NumberStyles.Number, CultureInfo.CurrentCulture, out double height))
        {
            throw new ArgumentException("Width and height must be numbers.");
        }

        return new DesktopWindowSize(
            Math.Clamp(width, 220, 1920),
            Math.Clamp(height, 160, 1200));
    }
}

public readonly record struct DesktopWindowSize(double Width, double Height);
