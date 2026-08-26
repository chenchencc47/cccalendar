using System.Drawing;
using System.IO;

namespace CcCalendar.Desktop;

public static class ApplicationIconLoader
{
    private const string ResourceName = "CcCalendar.Desktop.Assets.cccalendar.ico";

    public static Icon Load(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        using Stream stream = typeof(ApplicationIconLoader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The application icon resource is missing.");
        using var source = new Icon(stream, size, size);
        return (Icon)source.Clone();
    }
}
