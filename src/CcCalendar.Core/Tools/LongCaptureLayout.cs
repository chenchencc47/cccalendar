namespace CcCalendar.Core.Tools;

public sealed record LongCaptureLayout(IReadOnlyList<int> TopOffsets, int TotalHeight)
{
    public static LongCaptureLayout Create(IReadOnlyList<int> pageHeights, int overlap)
    {
        ArgumentNullException.ThrowIfNull(pageHeights);
        if (pageHeights.Count == 0 || pageHeights.Any(height => height <= overlap))
        {
            throw new ArgumentOutOfRangeException(nameof(pageHeights));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(overlap);
        var offsets = new int[pageHeights.Count];
        int cursor = 0;
        for (int index = 0; index < pageHeights.Count; index++)
        {
            offsets[index] = cursor;
            cursor += pageHeights[index] - (index == pageHeights.Count - 1 ? 0 : overlap);
        }

        return new LongCaptureLayout(offsets, cursor);
    }
}
