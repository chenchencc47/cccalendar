using CcCalendar.Core.Recycling;

namespace CcCalendar.Core.Projects;

public sealed class Project : IRecyclableEntity
{
    private readonly List<Milestone> milestones = [];
    private readonly List<Participant> participants = [];

    private Project()
    {
        Name = null!;
        Color = null!;
    }

    private Project(
        string name,
        string color,
        DateOnly? startDate,
        DateOnly? dueDate)
    {
        Id = Guid.NewGuid();
        Name = NormalizeRequiredText(name, nameof(name));
        Color = NormalizeColor(color);
        StartDate = startDate;
        DueDate = dueDate;
        Status = ProjectStatus.Planned;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Color { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public IReadOnlyList<Milestone> Milestones => milestones;

    public IReadOnlyList<Participant> Participants => participants;

    public static Project Create(
        string name,
        string color,
        DateOnly? startDate,
        DateOnly? dueDate)
    {
        if (startDate.HasValue && dueDate.HasValue && dueDate < startDate)
        {
            throw new ArgumentException("The due date cannot be before the start date.", nameof(dueDate));
        }

        return new Project(name, color, startDate, dueDate);
    }

    public Milestone AddMilestone(string title, DateOnly dueDate)
    {
        var milestone = new Milestone(title, dueDate);
        milestones.Add(milestone);
        return milestone;
    }

    public Participant AddParticipant(string displayName, string role)
    {
        string normalizedName = NormalizeRequiredText(displayName, nameof(displayName));

        if (participants.Any(participant =>
            string.Equals(participant.DisplayName, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A participant with the same display name already exists.");
        }

        var participant = new Participant(normalizedName, role);
        participants.Add(participant);
        return participant;
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        Status = ProjectStatus.Archived;
        ArchivedAtUtc = archivedAtUtc.ToUniversalTime();
    }

    public void MoveToRecycleBin(DateTimeOffset deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc.ToUniversalTime();
    }

    public void RestoreFromRecycleBin()
    {
        DeletedAtUtc = null;
    }

    private static string NormalizeColor(string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        if (color.Length != 7 || color[0] != '#' || !color.AsSpan(1).ContainsOnlyHexDigits())
        {
            throw new ArgumentException("The project color must use #RRGGBB format.", nameof(color));
        }

        return color.ToUpperInvariant();
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}

file static class CharacterSpanExtensions
{
    public static bool ContainsOnlyHexDigits(this ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
