namespace CcCalendar.Core.Recurrence;

public sealed class RecurrenceRule
{
    private RecurrenceRule(
        RecurrenceFrequency frequency,
        int interval,
        IReadOnlyList<DayOfWeek> daysOfWeek,
        DateOnly? untilInclusive,
        bool skipHolidays)
    {
        Frequency = frequency;
        Interval = interval;
        DaysOfWeek = daysOfWeek;
        UntilInclusive = untilInclusive;
        SkipHolidays = skipHolidays;
    }

    public RecurrenceFrequency Frequency { get; }

    public int Interval { get; }

    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; }

    public DateOnly? UntilInclusive { get; }

    public bool SkipHolidays { get; }

    public static RecurrenceRule Create(
        RecurrenceFrequency frequency,
        int interval,
        IEnumerable<DayOfWeek> daysOfWeek,
        DateOnly? untilInclusive,
        bool skipHolidays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, 1);
        ArgumentNullException.ThrowIfNull(daysOfWeek);

        DayOfWeek[] normalizedDays = [.. daysOfWeek.Distinct().OrderBy(day => day)];
        return new RecurrenceRule(frequency, interval, normalizedDays, untilInclusive, skipHolidays);
    }

    public static RecurrenceRule Workdays(DateOnly? untilInclusive)
    {
        return Create(
            RecurrenceFrequency.Daily,
            1,
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            untilInclusive,
            skipHolidays: true);
    }
}
