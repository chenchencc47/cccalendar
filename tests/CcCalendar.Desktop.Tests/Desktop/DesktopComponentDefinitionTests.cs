using System.Windows;
using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class DesktopComponentDefinitionTests
{
    [Theory]
    [InlineData(DesktopComponentKind.Calendar, 680, 460)]
    [InlineData(DesktopComponentKind.Agenda, 320, 300)]
    [InlineData(DesktopComponentKind.Todo, 320, 300)]
    public void ForKindReturnsStableDefaultSize(DesktopComponentKind kind, double width, double height)
    {
        DesktopComponentDefinition definition = DesktopComponentDefinition.ForKind(kind);

        Assert.Equal(new Size(width, height), definition.DefaultSize);
    }
}
