using CcCalendar.Core.Projects;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class StatisticsViewModelTests
{
    [Fact]
    public void LoadCalculatesCompletionOverdueAndWeeklyTrend()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        Project project = Project.Create("cccalendar", "#246BCE", null, null);
        TodoItem completed = TodoItem.Create("Done", project.Id, now.AddDays(-1));
        completed.Complete(now.AddDays(-1));
        TodoItem overdue = TodoItem.Create("Late", project.Id, now.AddHours(-1));
        var viewModel = new StatisticsViewModel(new FixedTimeProvider(now));

        viewModel.Load([project], [completed, overdue]);

        Assert.Equal(2, viewModel.TotalTodos);
        Assert.Equal(1, viewModel.CompletedTodos);
        Assert.Equal(1, viewModel.OverdueTodos);
        Assert.Equal(50, viewModel.CompletionPercent);
        Assert.Equal(7, viewModel.DailyCompletions.Count);
        Assert.Equal(1, viewModel.DailyCompletions.Sum(point => point.Count));
        Assert.Contains("完成 1 项", viewModel.WeeklySummary, StringComparison.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
