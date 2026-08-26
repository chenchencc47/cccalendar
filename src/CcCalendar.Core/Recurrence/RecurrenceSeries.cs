namespace CcCalendar.Core.Recurrence;

public sealed class RecurrenceSeries
{
    private readonly List<ExcludedOccurrence> exceptions = [];

    private RecurrenceSeries()
    {
    }

    private RecurrenceSeries(Guid calendarEventId, RecurrenceRule rule)
    {
        Id = Guid.NewGuid();
        CalendarEventId = calendarEventId;
        Frequency = rule.Frequency;
        Interval = rule.Interval;
        WeekdayMask = CreateWeekdayMask(rule.DaysOfWeek);
        UntilInclusive = rule.UntilInclusive;
        SkipHolidays = rule.SkipHolidays;
    }

    public Guid Id { get; private set; }

    public Guid CalendarEventId { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }

    public int Interval { get; private set; }

    public int WeekdayMask { get; private set; }

    public DateOnly? UntilInclusive { get; private set; }

    public bool SkipHolidays { get; private set; }

    public RecurrenceRule Rule => RecurrenceRule.Create(
        Frequency,
        Interval,
        ReadWeekdays(WeekdayMask),
        UntilInclusive,
        SkipHolidays);

    public IReadOnlySet<DateOnly> ExcludedDates => exceptions
        .Select(exception => exception.OccurrenceDate)
        .ToHashSet();

    public IReadOnlyList<ExcludedOccurrence> Exceptions => exceptions;

    public static RecurrenceSeries Create(Guid calendarEventId, RecurrenceRule rule)
    {
        if (calendarEventId == Guid.Empty)
        {
            throw new ArgumentException("A recurrence series must reference a calendar event.", nameof(calendarEventId));
        }

        ArgumentNullException.ThrowIfNull(rule);
        return new RecurrenceSeries(calendarEventId, rule);
    }

    public void Exclude(DateOnly occurrenceDate)
    {
        if (exceptions.Any(exception => exception.OccurrenceDate == occurrenceDate))
        {
            return;
        }

        exceptions.Add(new ExcludedOccurrence(occurrenceDate));
    }

    private static int CreateWeekdayMask(IEnumerable<DayOfWeek> daysOfWeek)
    {
        int mask = 0;

        foreach (DayOfWeek day in daysOfWeek)
        {
            mask |= 1 << (int)day;
        }

        return mask;
    }

    private static IEnumerable<DayOfWeek> ReadWeekdays(int mask)
    {
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if ((mask & (1 << (int)day)) != 0)
            {
                yield return day;
            }
        }
    }
}
