using System.Net;
using System.Security.Cryptography;
using System.Text;
using CcCalendar.Core.Security;
using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Infrastructure.Tests.Sync;

public sealed class OidcPkceTests
{
    [Fact]
    public void AuthorizationRequestUsesS256AndCarriesState()
    {
        PkceAuthorizationRequest request = PkceAuthorizationRequestFactory.Create(
            new OidcClientOptions(
                new Uri("https://login.example.test/authorize"),
                new Uri("https://login.example.test/token"),
                "cccalendar-desktop",
                new Uri("http://127.0.0.1:49152/callback"),
                ["openid", "profile"]),
            state: "state-123",
            codeVerifier: "verifier-123456789012345678901234567890");

        Assert.Contains("response_type=code", request.AuthorizationUri.Query);
        Assert.Contains("code_challenge_method=S256", request.AuthorizationUri.Query);
        Assert.Contains("state=state-123", request.AuthorizationUri.Query);
        Assert.Equal("state-123", request.State);
        Assert.Equal(
            Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(request.CodeVerifier))),
            request.CodeChallenge);
    }

    [Fact]
    public async Task TokenClientExchangesAuthorizationCode()
    {
        var handler = new RecordingHandler(
            "{\"access_token\":\"access-1\",\"refresh_token\":\"refresh-1\",\"token_type\":\"Bearer\",\"expires_in\":3600}");
        var client = new OidcTokenClient(new HttpClient(handler));

        OidcTokenSet tokens = await client.ExchangeCodeAsync(
            new Uri("https://login.example.test/token"),
            "cccalendar-desktop",
            "auth-code",
            "http://127.0.0.1:49152/callback",
            "verifier-1",
            CancellationToken.None);

        Assert.Equal("access-1", tokens.AccessToken);
        Assert.Equal("refresh-1", tokens.RefreshToken);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Contains("grant_type=authorization_code", handler.Body);
        Assert.Contains("code_verifier=verifier-1", handler.Body);
    }

    [Fact]
    public async Task AccessTokenProviderRefreshesExpiredTokenAndKeepsRefreshToken()
    {
        var secretStore = new MemorySecretStore(
            "{\"accessToken\":\"expired\",\"refreshToken\":\"refresh-old\",\"tokenType\":\"Bearer\",\"expiresAtUtc\":\"2020-01-01T00:00:00+00:00\"}");
        var handler = new RecordingHandler(
            "{\"access_token\":\"access-new\",\"refresh_token\":null,\"token_type\":\"Bearer\",\"expires_in\":3600}");
        var options = new OidcClientOptions(
            new Uri("https://login.example.test/authorize"),
            new Uri("https://login.example.test/token"),
            "cccalendar-desktop",
            new Uri("http://127.0.0.1:49152/callback"),
            ["openid"]);
        var provider = new OidcAccessTokenProvider(
            options,
            new OidcTokenClient(new HttpClient(handler)),
            new OidcTokenStore(secretStore),
            "oidc.tokens");

        string? token = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("access-new", token);
        Assert.Contains("refresh-old", secretStore.Value);
        Assert.Contains("grant_type=refresh_token", handler.Body);
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class MemorySecretStore(string? value) : ISecretStore
    {
        public string? Value { get; private set; } = value;

        public Task<string?> ReadAsync(string identifier, CancellationToken cancellationToken)
        {
            return Task.FromResult(Value);
        }

        public Task WriteAsync(string identifier, string secret, CancellationToken cancellationToken)
        {
            Value = secret;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string identifier, CancellationToken cancellationToken)
        {
            Value = null;
            return Task.CompletedTask;
        }
    }
}
