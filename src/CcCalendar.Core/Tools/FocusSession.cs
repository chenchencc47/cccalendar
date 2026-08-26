namespace CcCalendar.Core.Tools;

public sealed class FocusSession
{
    private FocusSession()
    {
    }

    private FocusSession(DateTimeOffset startedAtUtc, DateTimeOffset endedAtUtc)
    {
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        EndedAtUtc = endedAtUtc.ToUniversalTime();
        if (EndedAtUtc <= StartedAtUtc)
        {
            throw new ArgumentException("The focus session end must be after its start.", nameof(endedAtUtc));
        }

        Id = Guid.NewGuid();
        DurationMinutes = (int)Math.Round(
            (EndedAtUtc - StartedAtUtc).TotalMinutes,
            MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset EndedAtUtc { get; private set; }

    public int DurationMinutes { get; private set; }

    public static FocusSession Create(DateTimeOffset startedAtUtc, DateTimeOffset endedAtUtc)
    {
        return new FocusSession(startedAtUtc, endedAtUtc);
    }
}

public interface IFocusSessionStore
{
    Task AddAsync(FocusSession session, CancellationToken cancellationToken);

    Task<IReadOnlyList<FocusSession>> LoadAsync(CancellationToken cancellationToken);
}
