namespace CcCalendar.Core.Configuration;

public sealed record DesktopWindowBehaviorSettings
{
    public bool IsPositionLocked { get; init; }

    public bool IsMousePassthrough { get; init; }

    public DesktopWindowLayer Layer { get; init; } = DesktopWindowLayer.Normal;

    public bool IsEdgeAutoHideEnabled { get; init; }
}
