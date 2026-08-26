using System.IO;

namespace CcCalendar.Desktop;

public static class ApplicationPaths
{
    public static string RootDirectory => ResolveRootDirectory(
        Environment.GetEnvironmentVariable("CCCALENDAR_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string DatabasePath => Path.Combine(RootDirectory, "data", "calendar.db");

    public static string SettingsPath => Path.Combine(RootDirectory, "settings.json");

    public static string ClipboardStorageDirectory => Path.Combine(RootDirectory, "clipboard");

    public static string ResolveRootDirectory(string? configuredRoot, string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        return string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Path.GetFullPath(localApplicationData), "cccalendar")
            : Path.GetFullPath(configuredRoot);
    }
}
