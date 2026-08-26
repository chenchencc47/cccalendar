namespace CcCalendar.Desktop.Desktop;

public enum ShortcutConflictKind
{
    None,
    Invalid,
    Duplicate,
    System,
}

public sealed record ShortcutUpdateResult(
    ShortcutConflictKind Conflict,
    GlobalShortcutAction? ConflictingAction = null)
{
    public bool IsSuccess => Conflict == ShortcutConflictKind.None;

    public static ShortcutUpdateResult Success { get; } = new(ShortcutConflictKind.None);
}
