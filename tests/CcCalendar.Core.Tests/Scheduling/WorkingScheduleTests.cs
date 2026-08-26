using CcCalendar.Core.Scheduling;

namespace CcCalendar.Core.Tests.Scheduling;

public sealed class WorkingScheduleTests
{
    [Fact]
    public void DefaultScheduleContainsTwoWeekdayPeriodsAndNoWeekendPeriods()
    {
        WorkingSchedule schedule = WorkingSchedule.CreateDefault();

        IReadOnlyList<LocalTimeRange> monday = schedule.GetWorkingRanges(new DateOnly(2026, 8, 17));
        IReadOnlyList<LocalTimeRange> saturday = schedule.GetWorkingRanges(new DateOnly(2026, 8, 22));

        Assert.Equal(2, monday.Count);
        Assert.Equal(new TimeOnly(8, 0), monday[0].StartTime);
        Assert.Equal(new TimeOnly(12, 0), monday[0].EndTime);
        Assert.Equal(new TimeOnly(14, 0), monday[1].StartTime);
        Assert.Equal(new TimeOnly(17, 30), monday[1].EndTime);
        Assert.Empty(saturday);
    }

    [Fact]
    public void FindFreeRangesSubtractsBusyTimeFromWorkingPeriods()
    {
        WorkingSchedule schedule = WorkingSchedule.CreateDefault();
        var calendarDate = new DateOnly(2026, 8, 17);
        LocalTimeRange[] busyRanges =
        [
            new(calendarDate, new TimeOnly(9, 0), new TimeOnly(10, 0)),
            new(calendarDate, new TimeOnly(15, 0), new TimeOnly(16, 0)),
        ];

        IReadOnlyList<LocalTimeRange> free = schedule.FindFreeRanges(calendarDate, busyRanges);

        Assert.Equal(4, free.Count);
        Assert.Equal((new TimeOnly(8, 0), new TimeOnly(9, 0)), (free[0].StartTime, free[0].EndTime));
        Assert.Equal((new TimeOnly(10, 0), new TimeOnly(12, 0)), (free[1].StartTime, free[1].EndTime));
        Assert.Equal((new TimeOnly(14, 0), new TimeOnly(15, 0)), (free[2].StartTime, free[2].EndTime));
        Assert.Equal((new TimeOnly(16, 0), new TimeOnly(17, 30)), (free[3].StartTime, free[3].EndTime));
    }
}
