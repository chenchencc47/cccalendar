namespace CcCalendar.Core.Configuration;

public sealed record GlobalShortcutSettings
{
    public ShortcutGestureSettings OpenMainWindow { get; init; } = CreateDefault("C");

    public ShortcutGestureSettings ToggleQuickPanel { get; init; } = CreateDefault("Space");

    public ShortcutGestureSettings QuickAdd { get; init; } = CreateDefault("N");

    public ShortcutGestureSettings ToggleDesktopWorkbench { get; init; } = CreateDefault("D");

    public ShortcutGestureSettings DisableMousePassthrough { get; init; } = CreateDefault("P");

    private static ShortcutGestureSettings CreateDefault(string key)
    {
        return new ShortcutGestureSettings
        {
            Control = true,
            Alt = true,
            Shift = true,
            Key = key,
        };
    }
}
