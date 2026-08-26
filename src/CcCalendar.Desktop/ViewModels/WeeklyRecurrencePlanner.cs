namespace CcCalendar.Desktop.ViewModels;

/// <summary>
/// 按周重复：把所选周几展开为 [start, end] 闭区间内的具体日期（含两端）。
/// 用于"从 X 日到 Y 日每周几重复"的循环日程。
/// </summary>
public static class WeeklyRecurrencePlanner
{
    public static IReadOnlyList<DateOnly> GetDatesInRange(
        DateOnly start,
        DateOnly end,
        IReadOnlyList<DayOfWeek> weekdays)
    {
        ArgumentNullException.ThrowIfNull(weekdays);

        if (weekdays.Count == 0 || end < start)
        {
            return [];
        }

        var dates = new List<DateOnly>();
        for (DateOnly cursor = start; cursor <= end; cursor = cursor.AddDays(1))
        {
            if (weekdays.Contains(cursor.DayOfWeek))
            {
                dates.Add(cursor);
            }
        }

        return dates;
    }
}
