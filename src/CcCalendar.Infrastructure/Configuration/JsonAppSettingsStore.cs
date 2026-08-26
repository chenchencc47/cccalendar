using System.Text.Json;
using System.Text.Json.Serialization;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Infrastructure.Configuration;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string settingsPath;

    public JsonAppSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        this.settingsPath = Path.GetFullPath(settingsPath);
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            using FileStream stream = File.OpenRead(settingsPath);
            AppSettings? settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);

            return settings ?? throw new InvalidDataException("The settings file does not contain a settings object.");
        }
        catch (JsonException)
        {
            return RecoverCorruptSettings();
        }
        catch (InvalidDataException)
        {
            return RecoverCorruptSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string directoryPath = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directoryPath);

        string temporaryPath = $"{settingsPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private AppSettings RecoverCorruptSettings()
    {
        string backupPath = $"{settingsPath}.corrupt-{Guid.NewGuid():N}.json";
        try
        {
            File.Move(settingsPath, backupPath);
        }
        catch (IOException)
        {
            // Keep startup recoverable even when the original file cannot be moved.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep startup recoverable even when the original file cannot be moved.
        }

        return new AppSettings();
    }
}
