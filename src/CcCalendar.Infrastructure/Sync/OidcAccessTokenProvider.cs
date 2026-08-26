using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public sealed class OidcAccessTokenProvider(
    OidcClientOptions options,
    OidcTokenClient tokenClient,
    OidcTokenStore tokenStore,
    string tokenIdentifier) : IAccessTokenProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private OidcTokenSet? cachedTokens;

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (IsUsable(cachedTokens))
            {
                return cachedTokens!.AccessToken;
            }

            cachedTokens = await tokenStore.ReadAsync(tokenIdentifier, cancellationToken);
            if (IsUsable(cachedTokens))
            {
                return cachedTokens!.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(cachedTokens?.RefreshToken))
            {
                return null;
            }

            OidcTokenSet refreshed = await tokenClient.RefreshAsync(
                options.TokenEndpoint,
                options.ClientId,
                cachedTokens.RefreshToken,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(refreshed.RefreshToken))
            {
                refreshed = refreshed with { RefreshToken = cachedTokens.RefreshToken };
            }

            cachedTokens = refreshed;
            await tokenStore.WriteAsync(tokenIdentifier, refreshed, cancellationToken);
            return refreshed.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        gate.Dispose();
        await ValueTask.CompletedTask;
    }

    private static bool IsUsable(OidcTokenSet? tokens)
    {
        return tokens is not null
            && !string.IsNullOrWhiteSpace(tokens.AccessToken)
            && tokens.ExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(60);
    }
}
