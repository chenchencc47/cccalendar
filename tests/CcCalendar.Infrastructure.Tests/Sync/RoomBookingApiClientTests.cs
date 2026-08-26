using System.Net;
using System.Net.Http.Json;
using CcCalendar.Core.Rooms;
using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Infrastructure.Tests.Sync;

public sealed class RoomBookingApiClientTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid OrganizerId = Guid.NewGuid();

    [Fact]
    public async Task GetRoomsUsesWorkspaceRouteAndParsesCatalog()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new[]
        {
            new RoomCatalogEntry(RoomId, WorkspaceId, "项目组二楼会议室", "Asia/Shanghai", true),
        }));
        var client = new RoomBookingApiClient(CreateHttpClient(handler));

        IReadOnlyList<RoomCatalogEntry> rooms = await client.GetRoomsAsync(
            WorkspaceId,
            CancellationToken.None);

        RoomCatalogEntry room = Assert.Single(rooms);
        Assert.Equal(RoomId, room.Id);
        Assert.Equal($"/api/workspaces/{WorkspaceId}/rooms", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetBookingsQueriesRangeAndParsesResponse()
    {
        Guid bookingId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => JsonResponse(new[]
        {
            new RoomBookingResult(
                bookingId,
                WorkspaceId,
                RoomId,
                OrganizerId,
                "晨会",
                new DateTimeOffset(2026, 8, 21, 1, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 21, 2, 0, 0, TimeSpan.Zero),
                "UTC"),
        }));
        var client = new RoomBookingApiClient(CreateHttpClient(handler));

        DateTimeOffset from = new(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);
        IReadOnlyList<RoomBookingResult> bookings = await client.GetBookingsAsync(
            WorkspaceId,
            from,
            to,
            CancellationToken.None);

        RoomBookingResult booking = Assert.Single(bookings);
        Assert.Equal(bookingId, booking.Id);
        Assert.Equal(
            $"/api/workspaces/{WorkspaceId}/bookings?fromUtc={Uri.EscapeDataString(from.ToString("O"))}&toUtc={Uri.EscapeDataString(to.ToString("O"))}",
            handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task CreateBookingSendsIdempotencyKeyAndParsesResponse()
    {
        Guid bookingId = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("retry-key", request.Headers.GetValues("Idempotency-Key").Single());
            return JsonResponse(new RoomBookingResult(
                bookingId,
                WorkspaceId,
                RoomId,
                OrganizerId,
                "评审",
                new DateTimeOffset(2026, 8, 21, 1, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 21, 2, 0, 0, TimeSpan.Zero),
                "Asia/Shanghai"));
        });
        var client = new RoomBookingApiClient(CreateHttpClient(handler));

        RoomBookingResult result = await client.CreateBookingAsync(
            WorkspaceId,
            new RoomBookingRequest(
                RoomId,
                OrganizerId,
                "评审",
                new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.FromHours(8)),
                new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.FromHours(8)),
                "Asia/Shanghai"),
            "retry-key",
            CancellationToken.None);

        Assert.Equal(bookingId, result.Id);
        Assert.Equal($"/api/workspaces/{WorkspaceId}/bookings", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task ConflictResponsePreservesHttpStatusCode()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { code = "room_booking_conflict" }),
        });
        var client = new RoomBookingApiClient(CreateHttpClient(handler));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.CreateBookingAsync(
            WorkspaceId,
            new RoomBookingRequest(
                RoomId,
                OrganizerId,
                "冲突",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                "UTC"),
            "conflict-key",
            CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task RoomCatalogCacheAvoidsSecondRequestUntilForced()
    {
        int requestCount = 0;
        var handler = new RecordingHandler(_ =>
        {
            requestCount++;
            return JsonResponse(Array.Empty<RoomCatalogEntry>());
        });
        var client = new RoomBookingApiClient(CreateHttpClient(handler));
        var cache = new RoomCatalogCache();

        await cache.GetAsync(client, WorkspaceId, false, CancellationToken.None);
        await cache.GetAsync(client, WorkspaceId, false, CancellationToken.None);
        await cache.GetAsync(client, WorkspaceId, true, CancellationToken.None);

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task AccessTokenProviderAddsBearerHeader()
    {
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-token", request.Headers.Authorization?.Parameter);
            return JsonResponse(Array.Empty<RoomCatalogEntry>());
        });
        var client = new RoomBookingApiClient(
            CreateHttpClient(handler),
            new StaticAccessTokenProvider("access-token"));

        await client.GetRoomsAsync(WorkspaceId, CancellationToken.None);
    }

    private static HttpClient CreateHttpClient(RecordingHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://calendar.test/"),
        };
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value),
        };
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class StaticAccessTokenProvider(string token) : IAccessTokenProvider
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(token);
        }
    }
}
