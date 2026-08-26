namespace CcCalendar.Core.Configuration;

public static class DesktopAppearanceDefaults
{
    public static DesktopAppearanceSettings ForTheme(ThemePreference theme)
    {
        return theme == ThemePreference.Dark
            ? CreateDark()
            : new DesktopAppearanceSettings { Theme = theme };
    }

    private static DesktopAppearanceSettings CreateDark()
    {
        return new DesktopAppearanceSettings
        {
            Theme = ThemePreference.Dark,
            BackgroundColor = "#22252A",
            PrimaryTextColor = "#F0F2F4",
            SecondaryTextColor = "#A8AFB8",
            DateTextColor = "#F0F2F4",
            LunarTextColor = "#9CA4B0",
            WeekendTextColor = "#FC7B82",
            HolidayTextColor = "#5794E6",
            DayOffBackgroundColor = "#203957",
            DayOffTextColor = "#70A9F2",
            WorkdayBackgroundColor = "#4A292D",
            WorkdayTextColor = "#F07A82",
            TodayBackgroundColor = "#5794E6",
            TodayTextColor = "#FFFFFF",
            SeparatorColor = "#383D45",
        };
    }
}
