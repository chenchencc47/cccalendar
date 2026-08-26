namespace CcCalendar.Core.Configuration;

public sealed record DesktopAppearanceSettings
{
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public DesktopBackgroundMaterial Material { get; init; } = DesktopBackgroundMaterial.Solid;

    public double Opacity { get; init; } = 0.95;

    public double CornerRadius { get; init; } = 6;

    public double Scale { get; init; } = 1;

    public string FontFamily { get; init; } = "Segoe UI Variable, Microsoft YaHei UI";

    public double FontSize { get; init; } = 14;

    public bool IsTextBold { get; init; }

    public DesktopDensity Density { get; init; } = DesktopDensity.Comfortable;

    public bool ShowDateEvents { get; init; } = true;

    public bool ShowDetailedEvents { get; init; } = true;

    public bool ShowDateSeparators { get; init; } = true;

    public bool HighlightCurrentMonth { get; init; } = true;

    public bool ShowTextOutline { get; init; }

    public bool UseCustomColors { get; init; }

    public string BackgroundColor { get; init; } = "#FFFFFF";

    public string PrimaryTextColor { get; init; } = "#20242A";

    public string SecondaryTextColor { get; init; } = "#626A75";

    public string DateTextColor { get; init; } = "#20242A";

    public string LunarTextColor { get; init; } = "#7A8390";

    public string WeekendTextColor { get; init; } = "#C43D4B";

    public string HolidayTextColor { get; init; } = "#246BCE";

    public string DayOffBackgroundColor { get; init; } = "#E8F0FB";

    public string DayOffTextColor { get; init; } = "#246BCE";

    public string WorkdayBackgroundColor { get; init; } = "#FDECEC";

    public string WorkdayTextColor { get; init; } = "#C43D4B";

    public string TodayBackgroundColor { get; init; } = "#246BCE";

    public string TodayTextColor { get; init; } = "#FFFFFF";

    public string SeparatorColor { get; init; } = "#D9DEE5";
}
