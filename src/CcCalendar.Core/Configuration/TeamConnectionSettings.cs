namespace CcCalendar.Core.Configuration;

public sealed record TeamConnectionSettings
{
    public string ApiBaseUrl { get; init; } = string.Empty;

    public string AuthorizationEndpoint { get; init; } = string.Empty;

    public string TokenEndpoint { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string WorkspaceId { get; init; } = string.Empty;

    public string DevTokenName { get; init; } = string.Empty;

    /// <summary>开发令牌共享口令（公网部署时在服务端配置，登录需携带；空则不校验）。</summary>
    public string DevTokenSharedSecret { get; init; } = string.Empty;

    public string CurrentUserId { get; init; } = string.Empty;

    /// <summary>
    /// 解析当前连接模式对应的令牌存储标识：
    /// 缺少 OIDC 端点视为开发令牌模式（dev.tokens），否则按客户端 ID 存 oidc.tokens{.ClientId}。
    /// </summary>
    public string ResolveTokenIdentifier()
    {
        if (string.IsNullOrWhiteSpace(AuthorizationEndpoint)
            || string.IsNullOrWhiteSpace(TokenEndpoint))
        {
            return "dev.tokens";
        }

        return string.IsNullOrWhiteSpace(ClientId)
            ? "oidc.tokens"
            : $"oidc.tokens.{ClientId.Trim()}";
    }
}
