using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CcCalendar.Core.Sync;
using CcCalendar.Core.Workspaces;
using CcCalendar.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CcCalendar.Server.Tests;

public sealed class BookingApiTests : IClassFixture<TestServerFactory>
{
    internal static readonly Guid WorkspaceId = Guid.NewGuid();
    internal static readonly Guid ViewerWorkspaceId = Guid.NewGuid();
    internal static readonly Guid AdminWorkspaceId = Guid.NewGuid();
    internal static readonly Guid OtherWorkspaceId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid OrganizerId = TestIdentity.UserId;
    private static readonly Guid ViewerId = Guid.Parse("2a36fa2b-0a80-4ea9-82d5-2c4ca3e55d3a");
    private static readonly Guid AdminId = Guid.Parse("5caeab32-5f91-4b54-9b7c-e4e7bd9d6f6c");
    private static readonly Guid UnaffiliatedId = Guid.Parse("be5e7b56-11cb-45bc-9199-4bc5c7be4ef3");

    private readonly HttpClient client;
    private readonly TestServerFactory factory;

    public BookingApiTests(TestServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task RoomCatalogEndpointReturnsAnArray()
    {
        using HttpResponseMessage response = await client.GetAsync(
            $"/api/workspaces/{WorkspaceId}/rooms",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RoomCatalogResponse[] rooms = await response.Content.ReadFromJsonAsync<RoomCatalogResponse[]>() ?? [];
        Assert.Empty(rooms);
    }

    [Fact]
    public async Task AnonymousRequestIsRejected()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/workspaces/{WorkspaceId}/rooms");
        request.Headers.Add("X-Test-Anonymous", "1");

        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpointIsReachableWithoutAuthentication()
    {
        using HttpResponseMessage response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HealthResponse? health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
    }

    [Fact]
    public async Task AnonymousSignalRNegotiationIsRejected()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/hubs/sync/negotiate?negotiateVersion=1");
        request.Headers.Add("X-Test-Anonymous", "1");

        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignedInUserWithoutMembershipCannotReadRooms()
    {
        using HttpRequestMessage request = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{OtherWorkspaceId}/rooms",
            UnaffiliatedId);

        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ViewerCanReadRoomsButCannotCreateBooking()
    {
        using HttpRequestMessage catalogRequest = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{ViewerWorkspaceId}/rooms",
            ViewerId);
        using HttpResponseMessage catalogResponse = await client.SendAsync(catalogRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);

        using HttpRequestMessage bookingRequest = AsUser(
            HttpMethod.Post,
            $"/api/workspaces/{ViewerWorkspaceId}/bookings",
            ViewerId,
            JsonContent.Create(Request("viewer-booking", 8, 9) with { OrganizerId = ViewerId }));
        bookingRequest.Headers.Add("Idempotency-Key", "viewer-booking");
        using HttpResponseMessage bookingResponse = await client.SendAsync(bookingRequest, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, bookingResponse.StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateRoom()
    {
        using HttpRequestMessage request = AsUser(
            HttpMethod.Post,
            $"/api/workspaces/{AdminWorkspaceId}/rooms",
            AdminId,
            JsonContent.Create(new CreateRoomRequest("Board Room", "Asia/Shanghai")));

        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        RoomCatalogResponse? room = await response.Content.ReadFromJsonAsync<RoomCatalogResponse>();
        Assert.NotNull(room);
        Assert.Equal(AdminWorkspaceId, room.WorkspaceId);
        Assert.Equal("Board Room", room.Name);
    }

    [Fact]
    public async Task SyncCursorReturnsRoomCreationOnce()
    {
        using HttpRequestMessage create = AsUser(
            HttpMethod.Post,
            $"/api/workspaces/{AdminWorkspaceId}/rooms",
            AdminId,
            JsonContent.Create(new CreateRoomRequest("Sync Room", "UTC")));
        using HttpResponseMessage created = await client.SendAsync(create, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using HttpRequestMessage firstRequest = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{AdminWorkspaceId}/sync?cursor=0",
            AdminId);
        using HttpResponseMessage first = await client.SendAsync(firstRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        SyncPageResponse? page = await first.Content.ReadFromJsonAsync<SyncPageResponse>();
        Assert.NotNull(page);
        Assert.Contains(page.Changes, change => change.EntityType == "room");
        Assert.True(page.Cursor > 0);

        using HttpRequestMessage secondRequest = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{AdminWorkspaceId}/sync?cursor={page.Cursor}",
            AdminId);
        using HttpResponseMessage second = await client.SendAsync(secondRequest, CancellationToken.None);
        SyncPageResponse? empty = await second.Content.ReadFromJsonAsync<SyncPageResponse>();
        Assert.NotNull(empty);
        Assert.Empty(empty.Changes);
    }

    [Fact]
    public async Task MembershipDoesNotCrossWorkspaceBoundary()
    {
        using HttpRequestMessage request = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{OtherWorkspaceId}/rooms",
            OrganizerId);

        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TwoClientsShareRoomChangesAndServerConflictDecision()
    {
        using HttpClient secondClient = factory.CreateClient();
        using HttpRequestMessage createRoom = AsUser(
            HttpMethod.Post,
            $"/api/workspaces/{AdminWorkspaceId}/rooms",
            AdminId,
            JsonContent.Create(new CreateRoomRequest("Shared Room", "UTC")));
        using HttpResponseMessage roomResponse = await client.SendAsync(createRoom, CancellationToken.None);
        RoomCatalogResponse? room = await roomResponse.Content.ReadFromJsonAsync<RoomCatalogResponse>();
        Assert.NotNull(room);

        using HttpRequestMessage readRooms = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{AdminWorkspaceId}/rooms",
            AdminId);
        using HttpResponseMessage readResponse = await secondClient.SendAsync(readRooms, CancellationToken.None);
        RoomCatalogResponse[] rooms = await readResponse.Content.ReadFromJsonAsync<RoomCatalogResponse[]>() ?? [];
        Assert.Contains(rooms, item => item.Id == room.Id);

        BookingRequest booking = new(
            room.Id,
            AdminId,
            "Team sync",
            new DateTimeOffset(2026, 8, 23, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 23, 14, 0, 0, TimeSpan.Zero),
            "UTC");
        using HttpRequestMessage firstBooking = AsUser(
            HttpMethod.Post,
            $"/api/workspaces/{AdminWorkspaceId}/bookings",
            AdminId,
            JsonContent.Create(booking));
        firstBooking.Headers.Add("Idempotency-Key", "two-client-a");
        using HttpResponseMessage firstBookingResponse = await client.SendAsync(firstBooking, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, firstBookingResponse.StatusCode);

        using HttpRequestMessage conflictingBooking = AsUser(
            HttpMethod.Post,
            $"/api/workspaces/{AdminWorkspaceId}/bookings",
            AdminId,
            JsonContent.Create(booking with { Title = "Team sync B" }));
        conflictingBooking.Headers.Add("Idempotency-Key", "two-client-b");
        using HttpResponseMessage conflictResponse = await secondClient.SendAsync(conflictingBooking, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task BookingForAnotherOrganizerIsForbidden()
    {
        using HttpResponseMessage response = await PostAsync(
            "booking-other-organizer",
            Request("not mine", 16, 17) with { OrganizerId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBookingReturnsCreatedResource()
    {
        using HttpResponseMessage response = await PostAsync(
            "booking-create-1",
            Request("first-booking", 9, 10));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        BookingResponse? booking = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);
        Assert.Equal(RoomId, booking.RoomId);
        Assert.Equal("测试用户", booking.OrganizerName);
    }

    [Fact]
    public async Task OverlappingBookingReturnsConflict()
    {
        using HttpResponseMessage first = await PostAsync("booking-conflict-1", Request("first", 11, 12));
        using HttpResponseMessage second = await PostAsync("booking-conflict-2", Request("second", 11, 12));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RetryingSameIdempotencyKeyReturnsTheOriginalBooking()
    {
        BookingRequest request = Request("retry", 14, 15);
        using HttpResponseMessage first = await PostAsync("booking-retry", request);
        using HttpResponseMessage second = await PostAsync("booking-retry", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        BookingResponse? firstBooking = await first.Content.ReadFromJsonAsync<BookingResponse>();
        BookingResponse? secondBooking = await second.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(firstBooking);
        Assert.NotNull(secondBooking);
        Assert.Equal(firstBooking.Id, secondBooking.Id);
    }

    [Fact]
    public async Task BookingRangeQueryReturnsOnlyBookingsIntersectingTheRange()
    {
        using HttpResponseMessage inside = await PostAsync("range-inside", Request("range-inside", 8, 9));
        using HttpResponseMessage overlapping = await PostAsync("range-overlap", Request("range-overlap", 12, 13));
        using HttpResponseMessage outside = await PostAsync(
            "range-outside",
            Request("range-outside", 20, 21) with
            {
                StartAt = new DateTimeOffset(2026, 8, 22, 20, 0, 0, TimeSpan.Zero),
                EndAt = new DateTimeOffset(2026, 8, 22, 21, 0, 0, TimeSpan.Zero),
            });
        Assert.Equal(HttpStatusCode.Created, inside.StatusCode);
        Assert.Equal(HttpStatusCode.Created, overlapping.StatusCode);
        Assert.Equal(HttpStatusCode.Created, outside.StatusCode);

        using HttpRequestMessage query = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{WorkspaceId}/bookings?fromUtc=2026-08-21T08:30:00Z&toUtc=2026-08-21T12:30:00Z",
            OrganizerId);
        using HttpResponseMessage response = await client.SendAsync(query, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        BookingResponse[] bookings = await response.Content.ReadFromJsonAsync<BookingResponse[]>() ?? [];
        DateTimeOffset from = new(2026, 8, 21, 8, 30, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 8, 21, 12, 30, 0, TimeSpan.Zero);
        Assert.All(bookings, booking =>
            Assert.True(booking.StartAtUtc < to && booking.EndAtUtc > from));
        Assert.Contains(bookings, booking => booking.Title == "range-inside");
        Assert.Contains(bookings, booking => booking.Title == "range-overlap");
        Assert.DoesNotContain(bookings, booking => booking.Title == "range-outside");
    }

    [Fact]
    public async Task BookingRangeQueryWithoutFromUtcIsRejected()
    {
        using HttpRequestMessage query = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{WorkspaceId}/bookings?toUtc=2026-08-21T12:30:00Z",
            OrganizerId);
        using HttpResponseMessage response = await client.SendAsync(query, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BookingRangeQueryRequiresMembership()
    {
        using HttpRequestMessage query = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{OtherWorkspaceId}/bookings?fromUtc=2026-08-21T00:00:00Z&toUtc=2026-08-22T00:00:00Z",
            OrganizerId);
        using HttpResponseMessage response = await client.SendAsync(query, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BookingCreatorCanDeleteAndSyncCursorContainsDeletion()
    {
        using HttpResponseMessage created = await PostAsync("delete-owned", Request("delete-owned", 18, 19));
        BookingResponse? booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);

        using HttpRequestMessage delete = AsUser(HttpMethod.Delete, $"/api/bookings/{booking.Id}", OrganizerId);
        using HttpResponseMessage deleted = await client.SendAsync(delete, CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using HttpRequestMessage query = AsUser(
            HttpMethod.Get,
            $"/api/workspaces/{WorkspaceId}/bookings?fromUtc=2026-08-21T00:00:00Z&toUtc=2026-08-22T00:00:00Z",
            OrganizerId);
        using HttpResponseMessage queried = await client.SendAsync(query, CancellationToken.None);
        BookingResponse[] bookings = await queried.Content.ReadFromJsonAsync<BookingResponse[]>() ?? [];
        Assert.DoesNotContain(bookings, item => item.Id == booking.Id);
    }

    [Fact]
    public async Task NonCreatorCannotDeleteBooking()
    {
        using HttpResponseMessage created = await PostAsync("delete-not-owned", Request("delete-not-owned", 19, 20));
        BookingResponse? booking = await created.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);

        using HttpRequestMessage delete = AsUser(HttpMethod.Delete, $"/api/bookings/{booking.Id}", ViewerId);
        using HttpResponseMessage response = await client.SendAsync(delete, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAsync(string idempotencyKey, BookingRequest request)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/workspaces/{WorkspaceId}/bookings")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(message, CancellationToken.None);
    }

    private static HttpRequestMessage AsUser(
        HttpMethod method,
        string uri,
        Guid userId,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Add("X-Test-User", userId.ToString());
        return request;
    }

    private static BookingRequest Request(string title, int startHour, int endHour)
    {
        return new BookingRequest(
            RoomId,
            OrganizerId,
            title,
            new DateTimeOffset(2026, 8, 21, startHour, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 21, endHour, 0, 0, TimeSpan.Zero),
            "UTC");
    }

    private sealed record BookingRequest(
        Guid RoomId,
        Guid OrganizerId,
        string Title,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        string TimeZoneId);

    private sealed record CreateRoomRequest(string Name, string TimeZoneId);

    private sealed record SyncPageResponse(long Cursor, SyncChangeResponse[] Changes);

    private sealed record SyncChangeResponse(
        Guid WorkspaceId,
        string EntityType,
        Guid EntityId,
        string Operation,
        long Version);

    private sealed record HealthResponse(string Status, string Service);

    private sealed record BookingResponse(
        Guid Id,
        Guid RoomId,
        Guid OrganizerId,
        string Title,
        DateTimeOffset StartAtUtc,
        DateTimeOffset EndAtUtc,
        string OrganizerName);

    private sealed record RoomCatalogResponse(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        string TimeZoneId,
        bool IsActive);
}

public sealed class TestServerFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var memberships = new InMemoryWorkspaceMembershipStore();
            memberships.Upsert(WorkspaceMembership.Create(
                BookingApiTests.WorkspaceId,
                TestIdentity.UserId,
                WorkspaceRole.Member,
                "测试用户"));
            memberships.Upsert(WorkspaceMembership.Create(
                BookingApiTests.ViewerWorkspaceId,
                Guid.Parse("2a36fa2b-0a80-4ea9-82d5-2c4ca3e55d3a"),
                WorkspaceRole.Viewer));
            memberships.Upsert(WorkspaceMembership.Create(
                BookingApiTests.AdminWorkspaceId,
                Guid.Parse("5caeab32-5f91-4b54-9b7c-e4e7bd9d6f6c"),
                WorkspaceRole.Admin));
            services.AddSingleton<IWorkspaceMembershipStore>(memberships);
            services.AddSingleton<IRoomBookingStore, InMemoryRoomBookingStore>();
            services.AddSingleton<IRoomCatalogStore, InMemoryRoomCatalogStore>();
            services.AddSingleton<IChangeNotifier, NoOpChangeNotifier>();
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }
}

internal sealed class NoOpChangeNotifier : IChangeNotifier
{
    public Task PublishAsync(SyncChange change, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal static class TestIdentity
{
    public static readonly Guid UserId = Guid.Parse("4c7d1f4c-61a9-4e7d-8f6a-c493a4be9f11");
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string subject = Request.Headers["X-Test-User"].FirstOrDefault()
            ?? TestIdentity.UserId.ToString();
        var identity = new ClaimsIdentity(
            [new Claim("sub", subject)],
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
