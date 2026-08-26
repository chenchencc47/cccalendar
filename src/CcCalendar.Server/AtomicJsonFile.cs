using System.Text.Json;

namespace CcCalendar.Server;

internal static class AtomicJsonFile
{
    public static void Write<T>(string filePath, T value, JsonSerializerOptions options)
    {
        string temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, options));
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
