using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CcCalendar.Server.Tests;

/// <summary>
/// docs/POSTGRES_ACCEPTANCE.md 的实机验收。
/// 仅当设置了 ConnectionStrings__Postgres 环境变量时执行真实数据库验证；
/// 未设置时直接通过，不阻塞无 PostgreSQL 环境的常规回归。
/// </summary>
public sealed class PostgresBookingAcceptanceTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");

    // 同一测试类内串行执行；工作区每次运行随机生成，避免与既有数据冲突。
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task HealthReportsPostgresStorage()
    {
        if (ConnectionString is null)
        {
            return;
        }

        using PostgresServerFactory factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HealthResponse? health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal("postgres", health.Storage);
    }

    [Fact]
    public async Task TwoClientsShareRoomCatalogAndConflictingBookingReturnsConflict()
    {
        if (ConnectionString is null)
        {
            return;
        }

        using PostgresServerFactory factory = CreateFactory();
        using HttpClient clientA = factory.CreateClient();
        using HttpClient clientB = factory.CreateClient();

        // 客户端 A 创建房间。
        Guid roomId = await CreateRoomAsync(clientA, "Acceptance Room");

        // 客户端 B 读取目录并看到该房间。
        using (HttpRequestMessage readRooms = AsAdmin(HttpMethod.Get, $"/api/workspaces/{WorkspaceId}/rooms"))
        {
            using HttpResponseMessage catalog = await clientB.SendAsync(readRooms, CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
            RoomCatalogResponse[] rooms = await catalog.Content.ReadFromJsonAsync<RoomCatalogResponse[]>() ?? [];
            Assert.Contains(rooms, item => item.Id == roomId);
        }

        // 客户端 A 预约 09:00-10:00 成功。
        DateTimeOffset start = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset end = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
        BookingRequest booking = new(roomId, AdminId, "Acceptance booking", start, end, "UTC");
        Guid bookingId;
        using (HttpRequestMessage first = AsAdmin(HttpMethod.Post, $"/api/workspaces/{WorkspaceId}/bookings"))
        {
            first.Content = JsonContent.Create(booking);
            first.Headers.Add("Idempotency-Key", $"two-clients-a-{WorkspaceId}");
            using HttpResponseMessage created = await clientA.SendAsync(first, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            BookingResponse? createdBooking = await created.Content.ReadFromJsonAsync<BookingResponse>();
            Assert.NotNull(createdBooking);
            bookingId = createdBooking.Id;
        }

        // 客户端 B 对同一时段预约得到 409 Conflict。
        using (HttpRequestMessage conflicting = AsAdmin(HttpMethod.Post, $"/api/workspaces/{WorkspaceId}/bookings"))
        {
            conflicting.Content = JsonContent.Create(booking with { Title = "Acceptance booking B" });
            conflicting.Headers.Add("Idempotency-Key", $"two-clients-b-{WorkspaceId}");
            using HttpResponseMessage conflict = await clientB.SendAsync(conflicting, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }

        // 客户端 A 用同一幂等键重试，返回原预约而不是重复创建。
        using (HttpRequestMessage retry = AsAdmin(HttpMethod.Post, $"/api/workspaces/{WorkspaceId}/bookings"))
        {
            retry.Content = JsonContent.Create(booking);
            retry.Headers.Add("Idempotency-Key", $"two-clients-a-{WorkspaceId}");
            using HttpResponseMessage retried = await clientA.SendAsync(retry, CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
            BookingResponse? original = await retried.Content.ReadFromJsonAsync<BookingResponse>();
            Assert.NotNull(original);
            Assert.Equal(bookingId, original.Id);
        }
    }

    [Fact]
    public async Task ServerRestartPreservesBookingsAndSyncChanges()
    {
        if (ConnectionString is null)
        {
            return;
        }

        DateTimeOffset start = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset end = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        Guid roomId;
        Guid bookingId;
        long cursorAfterFirstRun;

        using (PostgresServerFactory first = CreateFactory())
        using (HttpClient client = first.CreateClient())
        {
            roomId = await CreateRoomAsync(client, "Restart Room");
            bookingId = await CreateBookingAsync(client, roomId, "Restart booking", start, end, $"restart-create-{WorkspaceId}");

            using HttpRequestMessage pull = AsAdmin(
                HttpMethod.Get,
                $"/api/workspaces/{WorkspaceId}/sync?cursor=0");
            using HttpResponseMessage response = await client.SendAsync(pull, CancellationToken.None);
            SyncPageResponse? page = await response.Content.ReadFromJsonAsync<SyncPageResponse>();
            Assert.NotNull(page);
            Assert.Contains(page.Changes, change => change.EntityType == "room" && change.EntityId == roomId);
            Assert.Contains(page.Changes, change => change.EntityType == "room_booking" && change.EntityId == bookingId);
            cursorAfterFirstRun = page.Cursor;
        }

        // 重启：销毁原 Server 实例，用同一连接串新建实例。
        using PostgresServerFactory second = CreateFactory();
        using HttpClient restarted = second.CreateClient();

        // 原 cursor 之前的变更在重启后仍存在。
        using (HttpRequestMessage replay = AsAdmin(HttpMethod.Get, $"/api/workspaces/{WorkspaceId}/sync?cursor=0"))
        {
            using HttpResponseMessage replayResponse = await restarted.SendAsync(replay, CancellationToken.None);
            SyncPageResponse? replayed = await replayResponse.Content.ReadFromJsonAsync<SyncPageResponse>();
            Assert.NotNull(replayed);
            Assert.Contains(replayed.Changes, change => change.EntityType == "room" && change.EntityId == roomId);
            Assert.Contains(replayed.Changes, change => change.EntityType == "room_booking" && change.EntityId == bookingId);
        }

        // 已消费的 cursor 不会重复下发。
        using (HttpRequestMessage incremental = AsAdmin(
                   HttpMethod.Get,
                   $"/api/workspaces/{WorkspaceId}/sync?cursor={cursorAfterFirstRun}"))
        {
            using HttpResponseMessage empty = await restarted.SendAsync(incremental, CancellationToken.None);
            SyncPageResponse? emptyPage = await empty.Content.ReadFromJsonAsync<SyncPageResponse>();
            Assert.NotNull(emptyPage);
            Assert.Empty(emptyPage.Changes);
        }

        // 重启后预约依然生效：同房间同时段再次预约得到 409。
        using HttpRequestMessage conflicting = AsAdmin(HttpMethod.Post, $"/api/workspaces/{WorkspaceId}/bookings");
        conflicting.Content = JsonContent.Create(
            new BookingRequest(roomId, AdminId, "Conflicting after restart", start, end, "UTC"));
        conflicting.Headers.Add("Idempotency-Key", $"restart-conflict-{WorkspaceId}");
        using HttpResponseMessage conflict = await restarted.SendAsync(conflicting, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private static async Task<Guid> CreateRoomAsync(HttpClient client, string name)
    {
        using HttpRequestMessage request = AsAdmin(HttpMethod.Post, $"/api/workspaces/{WorkspaceId}/rooms");
        request.Content = JsonContent.Create(new CreateRoomRequest(name, "Asia/Shanghai"));
        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        RoomCatalogResponse? room = await response.Content.ReadFromJsonAsync<RoomCatalogResponse>();
        Assert.NotNull(room);
        return room.Id;
    }

    private static async Task<Guid> CreateBookingAsync(
        HttpClient client,
        Guid roomId,
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        string idempotencyKey)
    {
        using HttpRequestMessage request = AsAdmin(HttpMethod.Post, $"/api/workspaces/{WorkspaceId}/bookings");
        request.Content = JsonContent.Create(new BookingRequest(roomId, AdminId, title, start, end, "UTC"));
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        BookingResponse? booking = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);
        return booking.Id;
    }

    private static HttpRequestMessage AsAdmin(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Test-User", AdminId.ToString());
        return request;
    }

    private static PostgresServerFactory CreateFactory() => new(ConnectionString!, WorkspaceId, AdminId);

    private sealed class PostgresServerFactory(string connectionString, Guid workspaceId, Guid adminId)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Storage:Provider", "postgres");
            builder.UseSetting("Storage:ConnectionString", connectionString);
            builder.UseSetting("Bootstrap:Memberships:0:WorkspaceId", workspaceId.ToString());
            builder.UseSetting("Bootstrap:Memberships:0:UserId", adminId.ToString());
            builder.UseSetting("Bootstrap:Memberships:0:Role", "Admin");
            builder.ConfigureTestServices(services =>
            {
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

    private sealed record CreateRoomRequest(string Name, string TimeZoneId);

    private sealed record BookingRequest(
        Guid RoomId,
        Guid OrganizerId,
        string Title,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        string TimeZoneId);

    private sealed record BookingResponse(
        Guid Id,
        Guid RoomId,
        Guid OrganizerId,
        string Title,
        DateTimeOffset StartAtUtc,
        DateTimeOffset EndAtUtc);

    private sealed record RoomCatalogResponse(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        string TimeZoneId,
        bool IsActive);

    private sealed record SyncPageResponse(long Cursor, SyncChangeResponse[] Changes);

    private sealed record SyncChangeResponse(
        Guid WorkspaceId,
        string EntityType,
        Guid EntityId,
        string Operation,
        long Version);

    private sealed record HealthResponse(string Status, string Service, string Storage);
}
