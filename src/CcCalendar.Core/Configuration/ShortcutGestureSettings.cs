namespace CcCalendar.Core.Configuration;

public sealed record ShortcutGestureSettings
{
    public bool Control { get; init; }

    public bool Alt { get; init; }

    public bool Shift { get; init; }

    public bool Windows { get; init; }

    public string Key { get; init; } = string.Empty;

    public bool HasModifier => Control || Alt || Shift || Windows;
}
