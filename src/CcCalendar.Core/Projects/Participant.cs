namespace CcCalendar.Core.Projects;

public sealed class Participant
{
    private Participant()
    {
        DisplayName = null!;
        Role = null!;
    }

    internal Participant(string displayName, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        Id = Guid.NewGuid();
        DisplayName = displayName.Trim();
        Role = role.Trim();
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; }

    public string Role { get; private set; }
}
