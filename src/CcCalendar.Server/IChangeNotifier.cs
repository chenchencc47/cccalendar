using CcCalendar.Core.Sync;

namespace CcCalendar.Server;

public interface IChangeNotifier
{
    Task PublishAsync(SyncChange change, CancellationToken cancellationToken);
}
