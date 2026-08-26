using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

/// <summary>
/// 直接读取已持久化令牌的访问令牌提供者（开发令牌模式无刷新端点，
/// 每次请求从凭据存储读取当前令牌）。
/// </summary>
public sealed class StoredTokenAccessTokenProvider(
    OidcTokenStore tokenStore,
    Func<string> identifierProvider) : IAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        OidcTokenSet? tokens = await tokenStore.ReadAsync(
            identifierProvider(),
            cancellationToken);
        return string.IsNullOrWhiteSpace(tokens?.AccessToken)
            ? null
            : tokens.AccessToken;
    }
}
