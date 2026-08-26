using System.Runtime.CompilerServices;

namespace CcCalendar.Desktop;

internal static class WindowsEnvironmentBootstrap
{
    [ModuleInitializer]
    internal static void EnsureWindowsDirectoryVariable()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WINDIR")))
        {
            return;
        }

        string? systemRoot = Environment.GetEnvironmentVariable("SystemRoot");

        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            Environment.SetEnvironmentVariable("WINDIR", systemRoot);
        }
    }
}
