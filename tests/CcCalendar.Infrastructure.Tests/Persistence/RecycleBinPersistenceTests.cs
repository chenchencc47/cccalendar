using CcCalendar.Core.Todos;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class RecycleBinPersistenceTests
{
    [Fact]
    public async Task DeletedTodoIsHiddenAndCanBeRestored()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Guid todoId;
        var deletedAtUtc = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

        await using (var createContext = database.CreateContext())
        {
            TodoItem todo = TodoItem.Create("Recover me", null, null);
            todoId = todo.Id;
            createContext.Todos.Add(todo);
            await createContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (var deleteContext = database.CreateContext())
        {
            TodoItem todo = await deleteContext.Todos.SingleAsync(item => item.Id == todoId);
            todo.MoveToRecycleBin(deletedAtUtc);
            await deleteContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (var hiddenContext = database.CreateContext())
        {
            Assert.False(await hiddenContext.Todos.AnyAsync(item => item.Id == todoId));
            TodoItem deleted = await hiddenContext.Todos
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == todoId);
            deleted.RestoreFromRecycleBin();
            await hiddenContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var restoredContext = database.CreateContext();
        Assert.True(await restoredContext.Todos.AnyAsync(item => item.Id == todoId));
    }
}
