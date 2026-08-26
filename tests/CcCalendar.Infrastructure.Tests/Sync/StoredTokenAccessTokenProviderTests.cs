using CcCalendar.Core.Security;
using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Infrastructure.Tests.Sync;

public sealed class StoredTokenAccessTokenProviderTests
{
    [Fact]
    public async Task ReturnsStoredAccessTokenForCurrentIdentifier()
    {
        string currentIdentifier = "dev.tokens";
        var store = new FakeSecretStore();
        var provider = new StoredTokenAccessTokenProvider(
            new OidcTokenStore(store),
            () => currentIdentifier);
        await new OidcTokenStore(store).WriteAsync(
            "dev.tokens",
            new OidcTokenSet("token-value", null, "Bearer", DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None);

        string? token = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-value", token);
    }

    [Fact]
    public async Task ReturnsNullWhenNothingIsStoredForTheIdentifier()
    {
        var store = new FakeSecretStore();
        store.Secrets["oidc.tokens.other"] = "{}";
        var provider = new StoredTokenAccessTokenProvider(
            new OidcTokenStore(store),
            () => "dev.tokens");

        string? token = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Null(token);
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public Dictionary<string, string> Secrets { get; } = [];

        public Task<string?> ReadAsync(string identifier, CancellationToken cancellationToken)
        {
            return Task.FromResult(Secrets.TryGetValue(identifier, out string? secret) ? secret : null);
        }

        public Task WriteAsync(string identifier, string secret, CancellationToken cancellationToken)
        {
            Secrets[identifier] = secret;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string identifier, CancellationToken cancellationToken)
        {
            Secrets.Remove(identifier);
            return Task.CompletedTask;
        }
    }
}
