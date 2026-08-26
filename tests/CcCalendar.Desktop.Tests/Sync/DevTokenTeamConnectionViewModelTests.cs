using CcCalendar.Core.Configuration;
using CcCalendar.Core.Security;
using CcCalendar.Core.Sync;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Desktop.Tests.Sync;

public sealed class DevTokenTeamConnectionViewModelTests
{
    [Fact]
    public async Task DevTokenLoginPersistsTokenAndFillsWorkspaceId()
    {
        var secretStore = new MemorySecretStore();
        var tokenStore = new OidcTokenStore(secretStore);
        TeamConnectionSettings? saved = null;
        var settings = new TeamConnectionSettings
        {
            ApiBaseUrl = "http://192.168.1.88:5080/",
            DevTokenName = "张三",
            DevTokenSharedSecret = "team-passphrase-2026",
        };
        string? observedSecret = null;
        var viewModel = new TeamConnectionViewModel(
            settings,
            (_, _) => throw new InvalidOperationException("OIDC 登录不应被调用。"),
            tokenStore,
            updated => saved = updated,
            (_, name, sharedSecret, _) =>
            {
                observedSecret = sharedSecret;
                return Task.FromResult(new DevTokenLoginResult(
                    new OidcTokenSet("dev-access-1", null, "Bearer", DateTimeOffset.UtcNow.AddHours(8)),
                    Guid.Parse("2e9d4f8a-1111-4b6e-9c2d-7a3f5e8b1c40"),
                    Guid.Parse("1d0f6ad4-8c92-4f97-9d1c-6a1e5b7c9e10")));
            });

        await viewModel.LoginAsync(CancellationToken.None);

        Assert.True(viewModel.IsAuthenticated);
        Assert.Equal("已登录", viewModel.StatusMessage);
        Assert.Equal("team-passphrase-2026", observedSecret);
        Assert.Contains("dev-access-1", secretStore.Value);
        Assert.Equal(
            "1d0f6ad4-8c92-4f97-9d1c-6a1e5b7c9e10",
            viewModel.WorkspaceId);
        Assert.Equal(
            "2e9d4f8a-1111-4b6e-9c2d-7a3f5e8b1c40",
            viewModel.CurrentUserId);
        Assert.NotNull(saved);
        Assert.Equal("1d0f6ad4-8c92-4f97-9d1c-6a1e5b7c9e10", saved!.WorkspaceId);
        Assert.Equal("2e9d4f8a-1111-4b6e-9c2d-7a3f5e8b1c40", saved!.CurrentUserId);
    }

    [Fact]
    public async Task DevTokenLoginWithoutNameShowsError()
    {
        var viewModel = new TeamConnectionViewModel(
            new TeamConnectionSettings { ApiBaseUrl = "http://192.168.1.88:5080/" },
            (_, _) => throw new InvalidOperationException(),
            new OidcTokenStore(new MemorySecretStore()),
            _ => { },
            (_, _, _, _) => throw new InvalidOperationException("开发令牌登录不应被调用。"));

        await viewModel.LoginAsync(CancellationToken.None);

        Assert.False(viewModel.IsAuthenticated);
        Assert.Contains("姓名", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DevTokenLoginRefreshesStaleUserIdWhenNameChanges()
    {
        var tokenStore = new OidcTokenStore(new MemorySecretStore());
        var settings = new TeamConnectionSettings
        {
            ApiBaseUrl = "http://192.168.1.88:5080/",
            WorkspaceId = "old-workspace",
            CurrentUserId = "old-user",
            DevTokenName = "周家丞",
        };
        TeamConnectionSettings? saved = null;
        var viewModel = new TeamConnectionViewModel(
            settings,
            (_, _) => throw new InvalidOperationException(),
            tokenStore,
            updated => saved = updated,
            (_, _, _, _) => Task.FromResult(new DevTokenLoginResult(
                new OidcTokenSet("token", null, "Bearer", DateTimeOffset.UtcNow.AddHours(1)),
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))));

        await viewModel.LoginAsync(CancellationToken.None);

        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", viewModel.CurrentUserId);
        Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", viewModel.WorkspaceId);
        Assert.Equal(viewModel.CurrentUserId, saved!.CurrentUserId);
    }

    [Fact]
    public async Task DevTokenLoginWithoutServerAddressShowsError()
    {
        var viewModel = new TeamConnectionViewModel(
            new TeamConnectionSettings { DevTokenName = "张三" },
            (_, _) => throw new InvalidOperationException(),
            new OidcTokenStore(new MemorySecretStore()),
            _ => { },
            (_, _, _, _) => throw new InvalidOperationException());

        await viewModel.LoginAsync(CancellationToken.None);

        Assert.False(viewModel.IsAuthenticated);
        Assert.Contains("服务端地址", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DevTokenLogoutDeletesPersistedToken()
    {
        var secretStore = new MemorySecretStore("dev-token-persisted");
        var viewModel = new TeamConnectionViewModel(
            new TeamConnectionSettings
            {
                ApiBaseUrl = "http://192.168.1.88:5080/",
                DevTokenName = "张三",
            },
            (_, _) => throw new InvalidOperationException(),
            new OidcTokenStore(secretStore),
            _ => { },
            (_, _, _, _) => throw new InvalidOperationException());

        await viewModel.LogoutAsync(CancellationToken.None);

        Assert.False(viewModel.IsAuthenticated);
        Assert.Null(secretStore.Value);
    }

    [Fact]
    public async Task RestoreUsesPersistedUnexpiredTokenWithoutPrompting()
    {
        var secretStore = new MemorySecretStore();
        var tokenStore = new OidcTokenStore(secretStore);
        await tokenStore.WriteAsync(
            "dev.tokens",
            new OidcTokenSet("persisted", null, "Bearer", DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None);
        var viewModel = new TeamConnectionViewModel(
            new TeamConnectionSettings
            {
                ApiBaseUrl = "http://192.168.1.88:5080/",
                DevTokenName = "张三",
            },
            (_, _) => throw new InvalidOperationException(),
            tokenStore,
            _ => { },
            (_, _, _, _) => throw new InvalidOperationException("未过期令牌不应重新登录。"));

        await viewModel.RestoreAsync(CancellationToken.None);

        Assert.True(viewModel.IsAuthenticated);
        Assert.Equal("已登录", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RestoreRepairsCurrentUserIdFromPersistedDevToken()
    {
        var secretStore = new MemorySecretStore();
        var tokenStore = new OidcTokenStore(secretStore);
        string payload = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    "{\"sub\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        await tokenStore.WriteAsync(
            "dev.tokens",
            new OidcTokenSet(
                $"header.{payload}.signature",
                null,
                "Bearer",
                DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None);
        TeamConnectionSettings? saved = null;
        var viewModel = new TeamConnectionViewModel(
            new TeamConnectionSettings
            {
                ApiBaseUrl = "http://192.168.1.88:5080/",
                DevTokenName = "周家丞",
                CurrentUserId = "old-user",
            },
            (_, _) => throw new InvalidOperationException(),
            tokenStore,
            updated => saved = updated,
            (_, _, _, _) => throw new InvalidOperationException());

        await viewModel.RestoreAsync(CancellationToken.None);

        Assert.True(viewModel.IsAuthenticated);
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", viewModel.CurrentUserId);
        Assert.Equal(viewModel.CurrentUserId, saved!.CurrentUserId);
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
