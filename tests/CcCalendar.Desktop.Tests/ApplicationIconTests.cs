using System.IO;
using CcCalendar.Desktop;

namespace CcCalendar.Desktop.Tests;

public sealed class ApplicationIconTests
{
    [Fact]
    public void EmbeddedIconSupportsTrayAndExplorerSizes()
    {
        using System.Drawing.Icon small = ApplicationIconLoader.Load(16);

        Assert.Equal(16, small.Width);
        Assert.Equal(16, small.Height);

        using Stream stream = typeof(ApplicationIconLoader).Assembly.GetManifestResourceStream(
            "CcCalendar.Desktop.Assets.cccalendar.ico")!;
        using var reader = new BinaryReader(stream);
        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        int frameCount = reader.ReadUInt16();
        var sizes = new List<int>(frameCount);
        for (int index = 0; index < frameCount; index++)
        {
            byte width = reader.ReadByte();
            sizes.Add(width == 0 ? 256 : width);
            stream.Position += 15;
        }

        Assert.Contains(256, sizes);
    }
}
