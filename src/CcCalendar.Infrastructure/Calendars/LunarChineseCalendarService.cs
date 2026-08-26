using System.Globalization;
using CcCalendar.Core.Calendars;
using Lunar;
using Lunar.Util;
using LunarDate = Lunar.Lunar;

namespace CcCalendar.Infrastructure.Calendars;

public sealed class LunarChineseCalendarService : IChineseCalendarService
{
    public ChineseCalendarDayInfo GetDay(DateOnly calendarDate)
    {
        Solar solar = Solar.FromYmdHms(
            calendarDate.Year,
            calendarDate.Month,
            calendarDate.Day,
            12,
            0,
            0);
        LunarDate lunar = solar.Lunar;
        Holiday? holiday = HolidayUtil.GetHoliday(
            calendarDate.Year,
            calendarDate.Month,
            calendarDate.Day);
        string lunarText = lunar.Day == 1
            ? $"{lunar.MonthInChinese}月"
            : lunar.DayInChinese;
        string[] festivals =
        [
            .. solar.Festivals
                .Concat(lunar.Festivals)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal),
        ];

        return new ChineseCalendarDayInfo(
            calendarDate,
            lunarText,
            lunar.JieQi,
            festivals,
            holiday is null
                ? ChineseDayType.Regular
                : holiday.Work
                    ? ChineseDayType.AdjustedWorkday
                    : ChineseDayType.PublicHoliday,
            holiday?.Name ?? string.Empty,
            ParseRelatedDate(holiday?.Target),
            ISOWeek.GetWeekOfYear(calendarDate.ToDateTime(TimeOnly.MinValue)));
    }

    private static DateOnly? ParseRelatedDate(string? value)
    {
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly date)
            ? date
            : null;
    }
}
