using CcCalendar.Core.Recycling;
using CcCalendar.Core.Todos;

namespace CcCalendar.Core.Tests.Recycling;

public sealed class RecycleBinTests
{
    [Fact]
    public void MoveAndRestoreTodoPreservesEntity()
    {
        TodoItem todo = TodoItem.Create("Keep recoverable", null, null);
        var deletedAtUtc = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

        todo.MoveToRecycleBin(deletedAtUtc);
        Assert.True(todo.IsDeleted);
        Assert.Equal(deletedAtUtc, todo.DeletedAtUtc);

        todo.RestoreFromRecycleBin();
        Assert.False(todo.IsDeleted);
        Assert.Null(todo.DeletedAtUtc);
    }

    [Fact]
    public void PurgePolicyKeepsItemsUntilThirtyDaysHaveElapsed()
    {
        var deletedAtUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.False(RecycleBinPolicy.CanPurge(deletedAtUtc, deletedAtUtc.AddDays(29).AddHours(23)));
        Assert.True(RecycleBinPolicy.CanPurge(deletedAtUtc, deletedAtUtc.AddDays(30)));
    }
}
