namespace CcCalendar.Core.Recurrence;

public sealed class ExcludedOccurrence
{
    private ExcludedOccurrence()
    {
    }

    internal ExcludedOccurrence(DateOnly occurrenceDate)
    {
        Id = Guid.NewGuid();
        OccurrenceDate = occurrenceDate;
    }

    public Guid Id { get; private set; }

    public DateOnly OccurrenceDate { get; private set; }
}
