using CcCalendar.Core.Sync;
using Microsoft.AspNetCore.SignalR;

namespace CcCalendar.Server;

public sealed class SignalRChangeNotifier(IHubContext<SyncHub> hubContext) : IChangeNotifier
{
    public Task PublishAsync(SyncChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return hubContext.Clients
            .Group(SyncHub.GroupName(change.WorkspaceId))
            .SendAsync("syncChanged", change, cancellationToken);
    }
}
