using CcCalendar.Core.Auditing;

namespace CcCalendar.Core.Tests.Auditing;

public sealed class UndoManagerTests
{
    [Fact]
    public async Task UndoLastExecutesEachActionOnceInReverseOrder()
    {
        var calls = new List<string>();
        var manager = new UndoManager();
        manager.Register("first", _ =>
        {
            calls.Add("first");
            return Task.CompletedTask;
        });
        manager.Register("second", _ =>
        {
            calls.Add("second");
            return Task.CompletedTask;
        });

        string? secondDescription = await manager.UndoLastAsync(CancellationToken.None);
        string? firstDescription = await manager.UndoLastAsync(CancellationToken.None);
        string? noDescription = await manager.UndoLastAsync(CancellationToken.None);

        Assert.Equal(["second", "first"], calls);
        Assert.Equal("second", secondDescription);
        Assert.Equal("first", firstDescription);
        Assert.Null(noDescription);
    }
}
