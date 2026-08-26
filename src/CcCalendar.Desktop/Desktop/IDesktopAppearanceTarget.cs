using CcCalendar.Core.Configuration;

namespace CcCalendar.Desktop.Desktop;

public interface IDesktopAppearanceTarget
{
    DesktopAppearanceSettings AppearanceSettings { get; }

    void ApplyAppearance(DesktopAppearanceSettings settings);
}
