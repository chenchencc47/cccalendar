using CcCalendar.Core.Calendars;
using CcCalendar.Infrastructure.Calendars;

namespace CcCalendar.Infrastructure.Tests.Calendars;

public sealed class LunarChineseCalendarServiceTests
{
    private readonly LunarChineseCalendarService service = new();

    [Fact]
    public void LunarNewYearReturnsChineseLunarDateAndFestival()
    {
        ChineseCalendarDayInfo info = service.GetDay(new DateOnly(2026, 2, 17));

        Assert.Equal("正月", info.LunarText);
        Assert.Contains("春节", info.Festivals);
    }

    [Fact]
    public void StartOfAutumnReturnsSolarTerm()
    {
        ChineseCalendarDayInfo info = service.GetDay(new DateOnly(2026, 8, 7));

        Assert.Equal("立秋", info.SolarTerm);
    }

    [Fact]
    public void NationalDayReturnsPublicHolidayMetadata()
    {
        ChineseCalendarDayInfo info = service.GetDay(new DateOnly(2026, 10, 1));

        Assert.Equal(ChineseDayType.PublicHoliday, info.DayType);
        Assert.Equal("国庆节", info.HolidayName);
        Assert.Equal(new DateOnly(2026, 10, 1), info.RelatedHolidayDate);
    }

    [Fact]
    public void WeekendShiftReturnsAdjustedWorkdayMetadata()
    {
        ChineseCalendarDayInfo info = service.GetDay(new DateOnly(2026, 2, 14));

        Assert.Equal(ChineseDayType.AdjustedWorkday, info.DayType);
        Assert.Equal("春节", info.HolidayName);
        Assert.Equal(new DateOnly(2026, 2, 17), info.RelatedHolidayDate);
    }
}
