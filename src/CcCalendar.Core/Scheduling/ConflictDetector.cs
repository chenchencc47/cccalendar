namespace CcCalendar.Core.Scheduling;

public static class ConflictDetector
{
    public static IReadOnlyList<TimeRange> FindOverlaps(
        TimeRange candidate,
        IEnumerable<TimeRange> existingRanges)
    {
        ArgumentNullException.ThrowIfNull(existingRanges);

        return existingRanges
            .Where(existing =>
                candidate.StartAtUtc < existing.EndAtUtc
                && existing.StartAtUtc < candidate.EndAtUtc)
            .OrderBy(existing => existing.StartAtUtc)
            .ToArray();
    }
}
