using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace CcCalendar.Infrastructure.Updates;

public sealed record ApplicationUpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("mandatory")] bool Mandatory = false,
    [property: JsonPropertyName("releaseNotesUrl")] string? ReleaseNotesUrl = null,
    [property: JsonPropertyName("releaseNotes")] string? ReleaseNotes = null);

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

    public async Task<string> DownloadInstallerAsync(
        ApplicationUpdateManifest manifest,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (!Uri.TryCreate(manifest.Url, UriKind.Absolute, out Uri? downloadUri)
            || !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Update installers must be served over HTTPS.", nameof(manifest));
        }

        string expectedHash = manifest.Sha256.Trim();
        if (expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("更新清单中的 SHA-256 无效。");
        }

        Directory.CreateDirectory(destinationDirectory);
        string installerPath = Path.Combine(
            destinationDirectory,
            $"cccalendar-update-{Guid.NewGuid():N}.exe");

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                downloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (FileStream destination = new(
                installerPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            using FileStream downloadedInstaller = File.OpenRead(installerPath);
            string actualHash = Convert.ToHexString(await SHA256.HashDataAsync(
                downloadedInstaller,
                cancellationToken)).ToLowerInvariant();
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("下载的安装包校验失败。");
            }

            return installerPath;
        }
        catch
        {
            try
            {
                File.Delete(installerPath);
            }
            catch
            {
                // Best effort cleanup; preserve the original download error.
            }

            throw;
        }
    }
}
