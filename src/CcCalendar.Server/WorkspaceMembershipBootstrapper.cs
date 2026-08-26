using CcCalendar.Core.Workspaces;
using Microsoft.Extensions.Configuration;

namespace CcCalendar.Server;

public static class WorkspaceMembershipBootstrapper
{
    public static void Apply(IConfiguration configuration, IWorkspaceMembershipStore store)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(store);
        foreach (IConfigurationSection item in configuration.GetSection("Bootstrap:Memberships").GetChildren())
        {
            if (!Guid.TryParse(item["WorkspaceId"], out Guid workspaceId)
                || !Guid.TryParse(item["UserId"], out Guid userId)
                || !Enum.TryParse(item["Role"], ignoreCase: true, out WorkspaceRole role))
            {
                throw new InvalidOperationException(
                    $"Bootstrap membership '{item.Key}' must contain valid WorkspaceId, UserId and Role values.");
            }

            store.Upsert(WorkspaceMembership.Create(workspaceId, userId, role));
        }
    }
}
