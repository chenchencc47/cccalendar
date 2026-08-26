using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Desktop.Tests.Sync;

public sealed class DevTokenLoginClientTests
{
    [Fact]
    public async Task LoginPostsNameAndReturnsTokenResult()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """
            {
              "accessToken": "token-1",
              "tokenType": "Bearer",
              "userId": "8b41a4b7-9f23-4d1a-a5c8-2e7f6d3b1a90",
              "workspaceId": "1d0f6ad4-8c92-4f97-9d1c-6a1e5b7c9e10",
              "expiresAtUtc": "2026-08-21T12:00:00Z"
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5080/"),
        };
        var client = new DevTokenLoginClient(httpClient);

        DevTokenLoginResult result = await client.LoginAsync(
            new Uri("http://192.168.1.88:5080/"),
            "张三",
            sharedSecret: null,
            CancellationToken.None);

        Assert.Equal("token-1", result.Tokens.AccessToken);
        Assert.Null(result.Tokens.RefreshToken);
        Assert.Equal("Bearer", result.Tokens.TokenType);
        Assert.Equal(
            DateTimeOffset.Parse("2026-08-21T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            result.Tokens.ExpiresAtUtc);
        Assert.Equal(Guid.Parse("1d0f6ad4-8c92-4f97-9d1c-6a1e5b7c9e10"), result.WorkspaceId);
        Assert.Equal(
            "http://192.168.1.88:5080/api/auth/dev-token",
            handler.LastRequest?.RequestUri?.ToString());
        using JsonDocument document = JsonDocument.Parse(handler.LastBody ?? "{}");
        Assert.Equal("张三", document.RootElement.GetProperty("name").GetString());
        Assert.False(
            document.RootElement.TryGetProperty("sharedSecret", out _),
            "未提供口令时不应发送 sharedSecret 字段。");
    }

    [Fact]
    public async Task LoginPostsSharedSecretWhenProvided()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """
            {
              "accessToken": "token-2",
              "tokenType": "Bearer",
              "userId": "8b41a4b7-9f23-4d1a-a5c8-2e7f6d3b1a90",
              "workspaceId": "1d0f6ad4-8c92-4f97-9d1c-6a1e5b7c9e10",
              "expiresAtUtc": "2026-08-21T12:00:00Z"
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5080/"),
        };
        var client = new DevTokenLoginClient(httpClient);

        await client.LoginAsync(
            new Uri("http://192.168.1.88:5080/"),
            "李四",
            sharedSecret: "team-passphrase-2026",
            CancellationToken.None);

        using JsonDocument document = JsonDocument.Parse(handler.LastBody ?? "{}");
        Assert.Equal("李四", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            "team-passphrase-2026",
            document.RootElement.GetProperty("sharedSecret").GetString());
    }

    [Fact]
    public async Task LoginFailureThrowsHttpRequestException()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, """{"code":"invalid_name"}""");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5080/"),
        };
        var client = new DevTokenLoginClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.LoginAsync(
                new Uri("http://127.0.0.1:5080/"),
                "张三",
                sharedSecret: null,
                CancellationToken.None));
    }

    private sealed class StubHandler(HttpStatusCode status, string payload) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }
    }
}
