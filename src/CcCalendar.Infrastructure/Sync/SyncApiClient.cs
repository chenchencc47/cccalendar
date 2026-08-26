using System.Net.Http.Json;
using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public sealed class SyncApiClient : ISyncChangeClient
{
    private readonly HttpClient httpClient;
    private readonly IAccessTokenProvider? accessTokenProvider;

    public SyncApiClient(
        HttpClient httpClient,
        IAccessTokenProvider? accessTokenProvider = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.accessTokenProvider = accessTokenProvider;
        if (httpClient.BaseAddress is null)
        {
            throw new ArgumentException("The sync client requires a base address.", nameof(httpClient));
        }
    }

    public async Task<SyncPage> GetChangesAsync(
        Guid workspaceId,
        long cursor,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/workspaces/{workspaceId}/sync?cursor={Math.Max(cursor, 0)}");
        if (accessTokenProvider is not null)
        {
            string? token = await accessTokenProvider.GetAccessTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    token.Trim());
            }
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body)
                    ? $"The sync service returned {(int)response.StatusCode}."
                    : body,
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<SyncPage>(cancellationToken)
            ?? throw new InvalidDataException("The sync response was empty.");
    }
}
