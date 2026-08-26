using CcCalendar.Server;
using Microsoft.Extensions.Configuration;

namespace CcCalendar.Server.Tests;

public sealed class DeploymentOptionsTests
{
    [Fact]
    public void DefaultsToFileStorageForLanPrototype()
    {
        var configuration = new ConfigurationBuilder().Build();

        ServerDeploymentOptions options = ServerDeploymentOptions.From(configuration);

        Assert.Equal("file", options.StorageProvider);
        Assert.False(options.RequireHttps);
    }

    [Fact]
    public void PostgresStorageRequiresConnectionString()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "postgres",
            })
            .Build();

        ServerDeploymentOptions options = ServerDeploymentOptions.From(configuration);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void UnknownStorageProviderIsRejected()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "shared-folder",
            })
            .Build();

        ServerDeploymentOptions options = ServerDeploymentOptions.From(configuration);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
