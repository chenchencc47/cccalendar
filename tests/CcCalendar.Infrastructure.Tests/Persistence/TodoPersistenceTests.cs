using CcCalendar.Core.Projects;
using CcCalendar.Core.Todos;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class TodoPersistenceTests
{
    [Fact]
    public async Task TodoRoundTripsWithProjectQuadrantAndSubtasks()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var completedAtUtc = new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero);
        Guid todoId;

        await using (var writeContext = database.CreateContext())
        {
            Project project = Project.Create("cccalendar", "#246BCE", null, null);
            TodoItem prerequisite = TodoItem.Create("Define project", project.Id, completedAtUtc.AddHours(1));
            TodoItem todo = TodoItem.Create("Persist tasks", project.Id, completedAtUtc.AddDays(1));
            todo.MoveToQuadrant(TodoQuadrant.ImportantUrgent);
            todo.AddSubtask("Write migration").Complete(completedAtUtc);
            todo.SetPlanningWindow(completedAtUtc, completedAtUtc.AddDays(1));
            todo.AddDependency(prerequisite.Id);
            todoId = todo.Id;

            writeContext.AddRange(project, prerequisite, todo);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        TodoItem actual = await readContext.Todos
            .Include(todo => todo.Subtasks)
            .Include(todo => todo.Dependencies)
            .SingleAsync(todo => todo.Id == todoId);

        Assert.Equal(TodoQuadrant.ImportantUrgent, actual.GetQuadrant(completedAtUtc, TimeSpan.FromHours(24)));
        Assert.Single(actual.Subtasks);
        Assert.Single(actual.Dependencies);
        Assert.Equal(completedAtUtc, actual.StartAtUtc);
        Assert.Equal(100, actual.ProgressPercent);
    }
}
