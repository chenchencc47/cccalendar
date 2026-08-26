using CcCalendar.Core.QuickAdd;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class ScheduleEditorStateTests
{
    [Fact]
    public void TimeOptionsContainOnlyHalfHourChoices()
    {
        IReadOnlyList<ScheduleTimeOption> options = ScheduleTimeOption.CreateHalfHourOptions();

        Assert.Equal(48, options.Count);
        Assert.Contains(options, option => option.Value == new TimeOnly(14, 0));
        Assert.Contains(options, option => option.Value == new TimeOnly(14, 30));
        Assert.DoesNotContain(options, option => option.Value == new TimeOnly(14, 5));
    }

    [Fact]
    public void DefaultStartRoundsUpToHalfHour()
    {
        Assert.Equal(
            new TimeOnly(14, 30),
            ScheduleTimeOption.RoundUpToHalfHour(new TimeOnly(14, 5, 42)));
    }

    [Fact]
    public void ManualTimeTextAcceptsExactMinute()
    {
        Assert.Equal(new TimeOnly(14, 5), ScheduleTimeOption.ParseExactMinute("14:05"));
        Assert.Throws<ArgumentException>(() => ScheduleTimeOption.ParseExactMinute("14:5"));
    }

    [Fact]
    public void CreateRequestUsesSelectedLocalDateAndTimes()
    {
        var state = new ScheduleEditorState(
            new DateOnly(2026, 8, 20),
            new TimeOnly(14, 5),
            new TimeOnly(14, 10));
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

        QuickAddRequest request = state.CreateRequest("Project review", timeZone);

        Assert.Equal(QuickAddKind.Event, request.Kind);
        Assert.Equal(new DateTimeOffset(2026, 8, 20, 14, 5, 0, TimeSpan.FromHours(8)), request.StartAt);
        Assert.Equal(new DateTimeOffset(2026, 8, 20, 14, 10, 0, TimeSpan.FromHours(8)), request.EndAt);
        Assert.Equal("China Standard Time", request.TimeZoneId);
    }

    [Fact]
    public void EndTimeMustBeAfterStartTime()
    {
        var state = new ScheduleEditorState(
            new DateOnly(2026, 8, 20),
            new TimeOnly(11, 0),
            new TimeOnly(10, 0));

        Assert.Throws<ArgumentException>(() => state.CreateRequest("Invalid", TimeZoneInfo.Utc));
    }

    [Fact]
    public void AllDayRequestDoesNotRequireTimes()
    {
        var state = new ScheduleEditorState(
            new DateOnly(2026, 8, 20),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0));

        QuickAddRequest request = state.CreateAllDayRequest("No fixed time");

        Assert.Null(request.StartAt);
        Assert.Null(request.EndAt);
        Assert.Null(request.TimeZoneId);
        Assert.Equal(new DateOnly(2026, 8, 20), request.AllDayStart);
        Assert.Equal(new DateOnly(2026, 8, 21), request.AllDayEndExclusive);
    }
}
