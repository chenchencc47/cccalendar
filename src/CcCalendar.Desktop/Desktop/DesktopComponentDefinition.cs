using System.Windows;

namespace CcCalendar.Desktop.Desktop;

public sealed record DesktopComponentDefinition(
    DesktopComponentKind Kind,
    string Title,
    Size DefaultSize)
{
    public static DesktopComponentDefinition ForKind(DesktopComponentKind kind)
    {
        return kind switch
        {
            DesktopComponentKind.Calendar => new(kind, "桌面日历", new Size(680, 460)),
            DesktopComponentKind.Agenda => new(kind, "桌面日程", new Size(320, 300)),
            DesktopComponentKind.Todo => new(kind, "桌面待办", new Size(320, 300)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
