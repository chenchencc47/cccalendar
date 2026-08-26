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
            "ABC123"));
        var client = new ApplicationUpdateClient(httpClient);

        ApplicationUpdateManifest? result = await client.CheckAsync(
            new Uri("https://download.example.com/version.json"),
            new Version(0, 3, 5),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("0.3.6", result.Version);
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
}
