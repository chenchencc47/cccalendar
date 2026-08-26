using System.Net.Http.Json;
using CcCalendar.Core.Rooms;
using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public sealed class RoomBookingApiClient : IRoomBookingClient
{
    private readonly HttpClient httpClient;
    private readonly IAccessTokenProvider? accessTokenProvider;

    public RoomBookingApiClient(
        HttpClient httpClient,
        IAccessTokenProvider? accessTokenProvider = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.accessTokenProvider = accessTokenProvider;
        if (httpClient.BaseAddress is null)
        {
            throw new ArgumentException("The room booking client requires a base address.", nameof(httpClient));
        }
    }

    public async Task<IReadOnlyList<RoomCatalogEntry>> GetRoomsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/workspaces/{workspaceId}/rooms");
        await AddAuthorizationAsync(request, cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RoomCatalogEntry[]>(
                cancellationToken)
            ?? [];
    }

    public async Task<IReadOnlyList<RoomBookingResult>> GetBookingsAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        string query = $"api/workspaces/{workspaceId}/bookings"
            + $"?fromUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}"
            + $"&toUtc={Uri.EscapeDataString(toUtc.ToString("O"))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, query);
        await AddAuthorizationAsync(request, cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RoomBookingResult[]>(
                cancellationToken)
            ?? [];
    }

    public async Task<RoomBookingResult> CreateBookingAsync(
        Guid workspaceId,
        RoomBookingRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/workspaces/{workspaceId}/bookings")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey.Trim());
        await AddAuthorizationAsync(message, cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RoomBookingResult>(
                cancellationToken)
            ?? throw new InvalidDataException("The room booking response was empty.");
    }

    public async Task CancelBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"api/bookings/{bookingId}");
        await AddAuthorizationAsync(request, cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateBookingInvitationAsync(
        Guid bookingId,
        string meetingInvitationText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingInvitationText);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/bookings/{bookingId}")
        {
            Content = JsonContent.Create(new { meetingInvitationText }),
        };
        await AddAuthorizationAsync(request, cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task AddAuthorizationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (accessTokenProvider is null)
        {
            return;
        }

        string? token = await accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token.Trim());
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body)
                ? $"The room booking service returned {(int)response.StatusCode}."
                : body,
            null,
            response.StatusCode);
    }
}
