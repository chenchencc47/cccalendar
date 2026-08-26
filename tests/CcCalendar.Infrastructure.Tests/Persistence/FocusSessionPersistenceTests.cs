using CcCalendar.Core.Tools;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tests.Persistence;

public sealed class FocusSessionPersistenceTests
{
    [Fact]
    public async Task FocusSessionRoundTrips()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        FocusSession expected = FocusSession.Create(
            new DateTimeOffset(2026, 8, 16, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 16, 1, 25, 0, TimeSpan.Zero));
        await using (var write = database.CreateContext())
        {
            write.FocusSessions.Add(expected);
            await write.SaveChangesAsync(CancellationToken.None);
        }

        await using var read = database.CreateContext();
        FocusSession actual = await read.FocusSessions.SingleAsync();

        Assert.Equal(25, actual.DurationMinutes);
        Assert.Equal(expected.StartedAtUtc, actual.StartedAtUtc);
    }
}
