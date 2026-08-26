using CcCalendar.Core.Projects;
using CcCalendar.Core.Records;
using CcCalendar.Core.Schedules;
using CcCalendar.Core.Todos;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class GlobalSearchViewModelTests
{
    [Fact]
    public void SearchCombinesEntitiesAndAppliesKindFilter()
    {
        Project project = Project.Create("cccalendar", "#246BCE", null, null);
        TodoItem todo = TodoItem.Create("Calendar tests", project.Id, null);
        CalendarEvent calendarEvent = CalendarEvent.CreateAllDay(
            "Release",
            project.Id,
            new DateOnly(2026, 8, 16),
            new DateOnly(2026, 8, 17));
        WorkRecord record = WorkRecord.Create(
            WorkRecordType.WorkLog,
            "Log",
            project.Id,
            "calendar layout",
            new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero));
        var viewModel = new GlobalSearchViewModel();
        viewModel.Load([project], [todo], [calendarEvent], [record]);

        viewModel.Query = "calendar";
        Assert.Equal(3, viewModel.Results.Count);

        viewModel.KindFilter = GlobalSearchItemKind.Record;
        Assert.Single(viewModel.Results);
        Assert.Equal(record.Id, viewModel.Results[0].EntityId);
    }
}
