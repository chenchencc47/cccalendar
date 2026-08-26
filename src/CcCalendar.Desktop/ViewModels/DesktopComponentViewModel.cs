using CcCalendar.Core.Calendars;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.ViewModels;

public sealed class DesktopComponentViewModel
{
    public DesktopComponentViewModel(
        DesktopComponentKind kind,
        TimeProvider timeProvider,
        IChineseCalendarService? chineseCalendarService = null)
    {
        Kind = kind;
        Workbench = new DesktopWorkbenchViewModel(timeProvider, chineseCalendarService);
    }

    public DesktopComponentKind Kind { get; }

    public DesktopWorkbenchViewModel Workbench { get; }
}
