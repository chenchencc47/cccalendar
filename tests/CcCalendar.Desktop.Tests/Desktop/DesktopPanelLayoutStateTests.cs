using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class DesktopPanelLayoutStateTests
{
    [Fact]
    public void RememberBoundsPersistsIndependentSizeAndPosition()
    {
        var state = new DesktopPanelLayoutState(new DesktopPanelLayoutSettings
        {
            Width = 320,
            Height = 300,
        });

        state.RememberBounds(460, 240, 980, 120);

        Assert.Equal(
            new DesktopPanelLayoutSettings
            {
                Width = 460,
                Height = 240,
                Left = 980,
                Top = 120,
            },
            state.ToSettings());
    }
}
