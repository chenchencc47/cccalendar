using CcCalendar.Desktop.Tools;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class WindowPinControllerTests
{
    [Fact]
    public void RefreshFiltersOwnAndUntitledWindowsAndToggleCallsBackend()
    {
        var backend = new FakeWindowPinBackend(
        [
            new WindowPinTarget(new nint(1), 100, "cccalendar", false),
            new WindowPinTarget(new nint(2), 200, string.Empty, false),
            new WindowPinTarget(new nint(3), 300, "Notes", false),
        ]);
        var controller = new WindowPinController(backend, 100);

        controller.Refresh();
        controller.SelectedTarget = controller.Targets.Single();
        controller.ToggleSelected();
        controller.ToggleSelected();

        Assert.Equal("Notes", controller.Targets.Single().Title);
        Assert.Equal([(new nint(3), true), (new nint(3), false)], backend.Calls);
    }

    private sealed class FakeWindowPinBackend(IReadOnlyList<WindowPinTarget> targets) : IWindowPinBackend
    {
        public List<(nint Handle, bool Topmost)> Calls { get; } = [];

        public IReadOnlyList<WindowPinTarget> Enumerate() => targets;

        public void SetTopmost(nint handle, bool topmost) => Calls.Add((handle, topmost));
    }
}
