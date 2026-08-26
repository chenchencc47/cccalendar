using CcCalendar.Core.Scheduling;

namespace CcCalendar.Core.Tests.Scheduling;

public sealed class ConflictDetectorTests
{
    [Fact]
    public void FindOverlapsReturnsOnlyRangesWithSharedTime()
    {
        var start = new DateTimeOffset(2026, 8, 17, 6, 0, 0, TimeSpan.Zero);
        var candidate = new TimeRange(start, start.AddHours(1));
        TimeRange[] existing =
        [
            new(start.AddHours(-1), start),
            new(start.AddMinutes(30), start.AddHours(2)),
            new(start.AddHours(1), start.AddHours(2)),
        ];

        IReadOnlyList<TimeRange> conflicts = ConflictDetector.FindOverlaps(candidate, existing);

        Assert.Single(conflicts, existing[1]);
    }
}
