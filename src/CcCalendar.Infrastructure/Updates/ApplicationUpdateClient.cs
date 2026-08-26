using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CcCalendar.Infrastructure.Updates;

public sealed record ApplicationUpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("mandatory")] bool Mandatory = false,
    [property: JsonPropertyName("releaseNotesUrl")] string? ReleaseNotesUrl = null);

public sealed class ApplicationUpdateClient
{
    private readonly HttpClient httpClient;

    public ApplicationUpdateClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ApplicationUpdateManifest?> CheckAsync(
        Uri manifestUri,
        Version currentVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestUri);
        ArgumentNullException.ThrowIfNull(currentVersion);

        if (!string.Equals(manifestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Update manifests must be served over HTTPS.", nameof(manifestUri));
        }

        ApplicationUpdateManifest? manifest = await httpClient.GetFromJsonAsync<ApplicationUpdateManifest>(
            manifestUri,
            cancellationToken).ConfigureAwait(false);
        if (manifest is null
            || !Version.TryParse(manifest.Version, out Version? latestVersion)
            || latestVersion <= currentVersion
            || !Uri.TryCreate(manifest.Url, UriKind.Absolute, out Uri? downloadUri)
            || !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            return null;
        }

        return manifest;
    }
}
