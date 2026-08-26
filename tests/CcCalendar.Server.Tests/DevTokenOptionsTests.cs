using CcCalendar.Core.Workspaces;
using CcCalendar.Server;
using Microsoft.Extensions.Configuration;

namespace CcCalendar.Server.Tests;

public sealed class DevTokenOptionsTests
{
    [Fact]
    public void DefaultsToDisabled()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        DevTokenOptions options = DevTokenOptions.From(configuration);

        Assert.False(options.Enabled);
        Assert.Equal(WorkspaceRole.Admin, options.Role);
        Assert.Equal(480, options.AccessTokenLifetimeMinutes);
    }

    [Fact]
    public void EnabledRequiresSigningKey()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DevToken:Enabled"] = "true",
                ["Authentication:DevToken:WorkspaceId"] = Guid.NewGuid().ToString(),
            })
            .Build();

        DevTokenOptions options = DevTokenOptions.From(configuration);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void EnabledRequiresWorkspaceId()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DevToken:Enabled"] = "true",
                ["Authentication:DevToken:SigningKey"] = "dev-token-test-signing-key-0123456789abcdef",
            })
            .Build();

        DevTokenOptions options = DevTokenOptions.From(configuration);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ShortSigningKeyIsRejected()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DevToken:Enabled"] = "true",
                ["Authentication:DevToken:SigningKey"] = "too-short",
                ["Authentication:DevToken:WorkspaceId"] = Guid.NewGuid().ToString(),
            })
            .Build();

        DevTokenOptions options = DevTokenOptions.From(configuration);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void DevTokenCannotBeCombinedWithOidcAuthority()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://login.example.test/",
                ["Authentication:DevToken:Enabled"] = "true",
                ["Authentication:DevToken:SigningKey"] = "dev-token-test-signing-key-0123456789abcdef",
                ["Authentication:DevToken:WorkspaceId"] = Guid.NewGuid().ToString(),
            })
            .Build();

        DevTokenOptions options = DevTokenOptions.From(configuration);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ShortSharedSecretIsRejected()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DevToken:Enabled"] = "true",
                ["Authentication:DevToken:SigningKey"] = "dev-token-test-signing-key-0123456789abcdef",
                ["Authentication:DevToken:WorkspaceId"] = Guid.NewGuid().ToString(),
                ["Authentication:DevToken:SharedSecret"] = "short",
            })
            .Build();

        DevTokenOptions options = DevTokenOptions.From(configuration);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ValidConfigurationParsesRoleAndWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DevToken:Enabled"] = "true",
                ["Authentication:DevToken:SigningKey"] = "dev-token-test-signing-key-0123456789abcdef",
                ["Authentication:DevToken:WorkspaceId"] = workspaceId.ToString(),
                ["Authentication:DevToken:Role"] = "Member",
                ["Authentication:DevToken:AccessTokenLifetimeMinutes"] = "120",
                ["Authentication:DevToken:SharedSecret"] = "team-passphrase-2026",
            })
            .Build();

        DevTokenOptions options = DevTokenOptions.From(configuration);

        options.Validate();
        Assert.True(options.Enabled);
        Assert.Equal(workspaceId, options.WorkspaceId);
        Assert.Equal(WorkspaceRole.Member, options.Role);
        Assert.Equal(120, options.AccessTokenLifetimeMinutes);
        Assert.Equal("team-passphrase-2026", options.SharedSecret);
    }
}
