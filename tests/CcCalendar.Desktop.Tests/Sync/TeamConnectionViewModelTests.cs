using CcCalendar.Core.Configuration;
using CcCalendar.Core.Security;
using CcCalendar.Core.Sync;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Desktop.Tests.Sync;

public sealed class TeamConnectionViewModelTests
{
    [Fact]
    public async Task LoginPersistsTokenAndMarksConnectionAuthenticated()
    {
        var secretStore = new MemorySecretStore();
        var tokenStore = new OidcTokenStore(secretStore);
        var settings = new TeamConnectionSettings
        {
            ApiBaseUrl = "https://calendar.example.test/",
            AuthorizationEndpoint = "https://login.example.test/authorize",
            TokenEndpoint = "https://login.example.test/token",
            ClientId = "cccalendar-desktop",
        };
        var viewModel = new TeamConnectionViewModel(
            settings,
            (_, _) => Task.FromResult(new OidcTokenSet(
                "access-1",
                "refresh-1",
                "Bearer",
                DateTimeOffset.UtcNow.AddHours(1))),
            tokenStore,
            _ => { });

        await viewModel.LoginAsync(CancellationToken.None);

        Assert.True(viewModel.IsAuthenticated);
        Assert.Contains("access-1", secretStore.Value);
    }

    [Fact]
    public async Task LogoutDeletesPersistedToken()
    {
        var secretStore = new MemorySecretStore("existing");
        var viewModel = new TeamConnectionViewModel(
            new TeamConnectionSettings { ClientId = "client" },
            (_, _) => throw new InvalidOperationException(),
            new OidcTokenStore(secretStore),
            _ => { });

        await viewModel.LogoutAsync(CancellationToken.None);

        Assert.False(viewModel.IsAuthenticated);
        Assert.Null(secretStore.Value);
    }

    private sealed class MemorySecretStore(string? value = null) : ISecretStore
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
