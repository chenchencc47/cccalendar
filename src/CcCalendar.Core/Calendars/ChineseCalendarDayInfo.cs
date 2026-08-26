namespace CcCalendar.Core.Calendars;

public sealed record ChineseCalendarDayInfo(
    DateOnly Date,
    string LunarText,
    string SolarTerm,
    IReadOnlyList<string> Festivals,
    ChineseDayType DayType,
    string HolidayName,
    DateOnly? RelatedHolidayDate,
    int IsoWeek)
{
    public static ChineseCalendarDayInfo Empty(DateOnly date)
    {
        return new ChineseCalendarDayInfo(
            date,
            string.Empty,
            string.Empty,
            [],
            ChineseDayType.Regular,
            string.Empty,
            null,
            System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)));
    }
}
