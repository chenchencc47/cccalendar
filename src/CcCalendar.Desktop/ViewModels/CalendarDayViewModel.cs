using CcCalendar.Core.Calendars;

namespace CcCalendar.Desktop.ViewModels;

public sealed record CalendarDayViewModel(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    bool IsSelected,
    ChineseCalendarDayInfo ChineseCalendar,
    IReadOnlyList<CalendarDayEventViewModel> Events,
    int RemainingEventCount)
{
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public string LunarText => ChineseCalendar.LunarText;

    public string SecondaryText => !string.IsNullOrEmpty(ChineseCalendar.SolarTerm)
        ? ChineseCalendar.SolarTerm
        : ChineseCalendar.Festivals.Count > 0
            ? ChineseCalendar.Festivals[0]
            : !string.IsNullOrEmpty(ChineseCalendar.HolidayName)
                ? ChineseCalendar.HolidayName
                : ChineseCalendar.LunarText;

    public string DayBadgeText => ChineseCalendar.DayType switch
    {
        ChineseDayType.PublicHoliday => "休",
        ChineseDayType.AdjustedWorkday => "班",
        _ => string.Empty,
    };

    public bool IsPublicHoliday => ChineseCalendar.DayType == ChineseDayType.PublicHoliday;

    public bool IsAdjustedWorkday => ChineseCalendar.DayType == ChineseDayType.AdjustedWorkday;

    public bool HasSolarTerm => !string.IsNullOrEmpty(ChineseCalendar.SolarTerm);

    public bool HasFestival => ChineseCalendar.Festivals.Count > 0
        || !string.IsNullOrEmpty(ChineseCalendar.HolidayName);
}
