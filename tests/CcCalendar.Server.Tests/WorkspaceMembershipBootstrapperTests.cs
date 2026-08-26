using CcCalendar.Core.Workspaces;
using CcCalendar.Server;
using Microsoft.Extensions.Configuration;

namespace CcCalendar.Server.Tests;

public sealed class WorkspaceMembershipBootstrapperTests
{
    [Fact]
    public void SeedsConfiguredOwnerMembership()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:Memberships:0:WorkspaceId"] = workspaceId.ToString(),
                ["Bootstrap:Memberships:0:UserId"] = userId.ToString(),
                ["Bootstrap:Memberships:0:Role"] = "Owner",
            })
            .Build();
        var store = new InMemoryWorkspaceMembershipStore();

        WorkspaceMembershipBootstrapper.Apply(configuration, store);

        WorkspaceMembership? membership = store.Find(workspaceId, userId);
        Assert.NotNull(membership);
        Assert.Equal(WorkspaceRole.Owner, membership.Role);
    }

    [Fact]
    public void InvalidBootstrapIdentityFailsFast()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:Memberships:0:WorkspaceId"] = "not-a-guid",
                ["Bootstrap:Memberships:0:UserId"] = Guid.NewGuid().ToString(),
                ["Bootstrap:Memberships:0:Role"] = "Owner",
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => WorkspaceMembershipBootstrapper.Apply(
                configuration,
                new InMemoryWorkspaceMembershipStore()));
    }
}
