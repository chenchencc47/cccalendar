namespace CcCalendar.Core.Recurrence;

public interface IHolidayCalendar
{
    bool IsHoliday(DateOnly calendarDate);
}
