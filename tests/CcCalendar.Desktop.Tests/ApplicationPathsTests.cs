using System.IO;

namespace CcCalendar.Desktop.Tests;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void ConfiguredRootOverridesLocalApplicationData()
    {
        string configured = Path.Combine(Path.GetTempPath(), "cccalendar-isolated");

        string actual = ApplicationPaths.ResolveRootDirectory(
            configured,
            Path.Combine(Path.GetTempPath(), "local"));

        Assert.Equal(Path.GetFullPath(configured), actual);
    }
}
