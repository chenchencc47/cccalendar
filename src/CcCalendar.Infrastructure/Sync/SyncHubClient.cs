using CcCalendar.Core.Sync;
using Microsoft.AspNetCore.SignalR.Client;

namespace CcCalendar.Infrastructure.Sync;

public sealed class SyncHubClient : IAsyncDisposable
{
    private readonly HubConnection connection;
    private readonly IAccessTokenProvider accessTokenProvider;

    public SyncHubClient(Uri apiBaseAddress, IAccessTokenProvider accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(apiBaseAddress);
        this.accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        Uri hubAddress = new(apiBaseAddress, "hubs/sync");
        connection = new HubConnectionBuilder()
            .WithUrl(hubAddress, options =>
            {
                options.AccessTokenProvider = () => this.accessTokenProvider.GetAccessTokenAsync(CancellationToken.None);
            })
            .WithAutomaticReconnect()
            .Build();
        connection.On<SyncChange>("syncChanged", change => ChangeReceived?.Invoke(change));
    }

    public event Action<SyncChange>? ChangeReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    public Task JoinWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return connection.InvokeAsync("JoinWorkspace", workspaceId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}
