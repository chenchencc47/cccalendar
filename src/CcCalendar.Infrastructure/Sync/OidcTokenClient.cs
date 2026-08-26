using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public sealed class OidcTokenClient(HttpClient httpClient)
{
    public async Task<OidcTokenSet> ExchangeCodeAsync(
        Uri tokenEndpoint,
        string clientId,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenEndpoint);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
            ]),
        };
        return await SendAsync(request, cancellationToken);
    }

    public async Task<OidcTokenSet> RefreshAsync(
        Uri tokenEndpoint,
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenEndpoint);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            ]),
        };
        return await SendAsync(request, cancellationToken);
    }

    private async Task<OidcTokenSet> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body)
                    ? $"The OIDC token endpoint returned {(int)response.StatusCode}."
                    : body,
                null,
                response.StatusCode);
        }

        TokenResponse? payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidDataException("The OIDC token response did not contain an access token.");
        }

        return new OidcTokenSet(
            payload.AccessToken,
            payload.RefreshToken,
            string.IsNullOrWhiteSpace(payload.TokenType) ? "Bearer" : payload.TokenType,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(payload.ExpiresIn, 0)));
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
