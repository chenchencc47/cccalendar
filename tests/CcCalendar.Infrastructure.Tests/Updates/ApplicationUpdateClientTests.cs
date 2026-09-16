using System.Net;
using System.Net.Http.Json;
using CcCalendar.Infrastructure.Updates;

namespace CcCalendar.Infrastructure.Tests.Updates;

public sealed class ApplicationUpdateClientTests
{
    [Fact]
    public async Task ReturnsManifestWhenRemoteVersionIsNewer()
    {
        using var httpClient = CreateClient(new ApplicationUpdateManifest(
            "0.3.6",
            "https://download.example.com/cccalendar-0.3.6-win-x64-setup.exe",
            "ABC123",
            ReleaseNotes: "修复提醒问题"));
        var client = new ApplicationUpdateClient(httpClient);

        ApplicationUpdateManifest? result = await client.CheckAsync(
            new Uri("https://download.example.com/version.json"),
            new Version(0, 3, 5),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("0.3.6", result.Version);
        Assert.Equal("修复提醒问题", result.ReleaseNotes);
    }

    [Fact]
    public async Task IgnoresSameOrOlderVersions()
    {
        using var httpClient = CreateClient(new ApplicationUpdateManifest(
            "0.3.5",
            "https://download.example.com/cccalendar-0.3.5-win-x64-setup.exe",
            "ABC123"));
        var client = new ApplicationUpdateClient(httpClient);

        ApplicationUpdateManifest? result = await client.CheckAsync(
            new Uri("https://download.example.com/version.json"),
            new Version(0, 3, 5),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectsNonHttpsManifestEndpoint()
    {
        using var httpClient = new HttpClient();
        var client = new ApplicationUpdateClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.CheckAsync(
            new Uri("http://download.example.com/version.json"),
            new Version(0, 3, 5),
            CancellationToken.None));
    }

    [Fact]
    public async Task DownloadsInstallerAndVerifiesSha256()
    {
        byte[] payload = [1, 2, 3, 4, 5];
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
        var manifest = new ApplicationUpdateManifest(
            "0.3.6",
            "https://download.example.com/cccalendar-0.3.6-win-x64-setup.exe",
            hash);
        string directory = Path.Combine(Path.GetTempPath(), $"cccalendar-update-test-{Guid.NewGuid():N}");
        using var httpClient = new HttpClient(new BinaryHandler(payload));
        var client = new ApplicationUpdateClient(httpClient);

        try
        {
            string installerPath = await client.DownloadInstallerAsync(
                manifest,
                directory,
                CancellationToken.None);

            Assert.Equal(payload, await File.ReadAllBytesAsync(installerPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeletesInstallerWhenSha256DoesNotMatch()
    {
        var manifest = new ApplicationUpdateManifest(
            "0.3.6",
            "https://download.example.com/cccalendar-0.3.6-win-x64-setup.exe",
            new string('A', 64));
        string directory = Path.Combine(Path.GetTempPath(), $"cccalendar-update-test-{Guid.NewGuid():N}");
        using var httpClient = new HttpClient(new BinaryHandler([1, 2, 3]));
        var client = new ApplicationUpdateClient(httpClient);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadInstallerAsync(
                manifest,
                directory,
                CancellationToken.None));

            Assert.Empty(Directory.EnumerateFiles(directory, "*.exe"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// P67：读取清单时**不做版本过滤**。
    ///
    /// 修复前「设置 → 查看更新内容」显示的是一段写死在代码里的说明，且版本号停在
    /// 0.6.5——发版时没人会记得改它。清单本身就带 releaseNotes，直接取即可：
    /// 远程版本与当前版本相同时，那正是"当前版本"的说明。
    /// 因此需要一条不过滤版本的读取路径（CheckAsync 在远程不更新时返回 null）。
    /// </summary>
    [Fact]
    public async Task FetchManifestReturnsManifestEvenWhenRemoteIsNotNewer()
    {
        using var httpClient = CreateClient(new ApplicationUpdateManifest(
            "0.6.7",
            "https://download.example.com/cccalendar-0.6.7-win-x64-setup.exe",
            "ABC123",
            ReleaseNotes: "0.6.7 更新内容：界面改版"));
        var client = new ApplicationUpdateClient(httpClient);

        ApplicationUpdateManifest? manifest = await client.FetchManifestAsync(
            new Uri("https://download.example.com/version.json"),
            CancellationToken.None);

        Assert.NotNull(manifest);
        Assert.Equal("0.6.7", manifest.Version);
        Assert.Equal("0.6.7 更新内容：界面改版", manifest.ReleaseNotes);
    }

    /// <summary>非 HTTPS 的清单地址必须拒绝，与 CheckAsync 一致。</summary>
    [Fact]
    public async Task FetchManifestRejectsNonHttpsEndpoint()
    {
        using var httpClient = new HttpClient();
        var client = new ApplicationUpdateClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.FetchManifestAsync(
            new Uri("http://download.example.com/version.json"),
            CancellationToken.None));
    }
    private static HttpClient CreateClient(ApplicationUpdateManifest manifest)
    {
        var handler = new StubHandler(manifest);
        return new HttpClient(handler);
    }

    private sealed class StubHandler(ApplicationUpdateManifest manifest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(manifest),
            });
        }
    }

    private sealed class BinaryHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
        }
    }
}
