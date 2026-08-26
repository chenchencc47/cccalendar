using System.Net.Http.Json;
using System.Text.Json;
using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

/// <summary>
/// 局域网原型的开发令牌登录客户端：按姓名向服务端换取访问令牌。
/// </summary>
public sealed class DevTokenLoginClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<DevTokenLoginResult> LoginAsync(
        Uri apiBaseUrl,
        string name,
        string? sharedSecret,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apiBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Uri requestUri = new(apiBaseUrl, "api/auth/dev-token");
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            requestUri,
            new DevTokenLoginPayload(name.Trim(), string.IsNullOrWhiteSpace(sharedSecret) ? null : sharedSecret.Trim()),
            PayloadOptions,
            cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body)
                    ? $"开发令牌登录失败：{(int)response.StatusCode}"
                    : $"开发令牌登录失败：{body}",
                null,
                response.StatusCode);
        }

        DevTokenResponsePayload? payload = JsonSerializer.Deserialize<DevTokenResponsePayload>(body, JsonOptions);
        if (payload is null
            || string.IsNullOrWhiteSpace(payload.AccessToken)
            || string.IsNullOrWhiteSpace(payload.TokenType))
        {
            throw new InvalidDataException("开发令牌响应内容无效。");
        }

        return new DevTokenLoginResult(
            new OidcTokenSet(
                payload.AccessToken,
                RefreshToken: null,
                TokenType: payload.TokenType,
                ExpiresAtUtc: payload.ExpiresAtUtc),
            payload.UserId,
            payload.WorkspaceId);
    }

    private sealed record DevTokenLoginPayload(string Name, string? SharedSecret);

    private sealed record DevTokenResponsePayload(
        string AccessToken,
        string TokenType,
        Guid UserId,
        Guid WorkspaceId,
        DateTimeOffset ExpiresAtUtc);
}
