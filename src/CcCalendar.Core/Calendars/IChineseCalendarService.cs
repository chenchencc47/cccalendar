namespace CcCalendar.Core.Calendars;

public interface IChineseCalendarService
{
    ChineseCalendarDayInfo GetDay(DateOnly calendarDate);
}
