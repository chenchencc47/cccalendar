using CcCalendar.Core.Schedules;

namespace CcCalendar.Core.Tests.Schedules;

public sealed class TimeBlockTests
{
    [Fact]
    public void CreateAssociatesTimeRangeWithTodo()
    {
        Guid todoId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 8, 17, 14, 0, 0, TimeSpan.FromHours(8));

        TimeBlock block = TimeBlock.Create(todoId, start, start.AddHours(2), "China Standard Time");

        Assert.Equal(todoId, block.TodoItemId);
        Assert.Equal(TimeSpan.FromHours(2), block.Duration);
        Assert.Equal(start.ToUniversalTime(), block.StartAtUtc);
    }

    [Fact]
    public void CreateRejectsEmptyTodoAndInvalidRange()
    {
        var instant = new DateTimeOffset(2026, 8, 17, 14, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => TimeBlock.Create(Guid.Empty, instant, instant.AddHours(1), "UTC"));
        Assert.Throws<ArgumentException>(() => TimeBlock.Create(Guid.NewGuid(), instant, instant, "UTC"));
    }
}
