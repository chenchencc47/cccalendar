using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CcCalendar.Core.Rooms;
using CcCalendar.Core.Workspaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CcCalendar.Server.Tests;

/// <summary>
/// 开发令牌端到端验收：真实 JwtBearer 验证签发的 Bearer 令牌，
/// 开发登录自动加入配置的工作区，未携带令牌的请求被拒绝。
/// </summary>
public sealed class DevTokenAuthTests
{
    private const string SigningKey = "dev-token-test-signing-key-0123456789abcdef";
    private static readonly Guid WorkspaceId = Guid.Parse("1d0f6ad4-8c92-4f97-9d1c-6a1e5b7c9e10");

    [Fact]
    public async Task DevTokenEndpointIsNotMappedWhenDisabled()
    {
        using var factory = new DevTokenServerFactory(enabled: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/dev-token",
            new { name = "张三" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DevTokenLoginIssuesTokenThatAuthorizesRoomAndBookingAccess()
    {
        using var factory = new DevTokenServerFactory(enabled: true);
        using HttpClient client = factory.CreateClient();

        DevTokenLoginResponse login = await LoginAsync(client, "张三");
        Assert.NotEmpty(login.AccessToken);
        Assert.Equal(WorkspaceId, login.WorkspaceId);
        Assert.NotEqual(Guid.Empty, login.UserId);

        // 未携带令牌访问受保护端点被拒绝（真实 JwtBearer 生效）。
        using (HttpRequestMessage anonymous = new(HttpMethod.Get, $"/api/workspaces/{WorkspaceId}/rooms"))
        {
            using HttpResponseMessage anonymousResponse = await client.SendAsync(anonymous, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        }

        // 开发登录默认 Admin 角色：可创建房间。
        Guid roomId;
        using (HttpRequestMessage createRoom = new(HttpMethod.Post, $"/api/workspaces/{WorkspaceId}/rooms"))
        {
            createRoom.Content = JsonContent.Create(new CreateRoomRequest("Dev Room", "Asia/Shanghai"));
            createRoom.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage roomResponse = await client.SendAsync(createRoom, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Created, roomResponse.StatusCode);
            RoomCatalogEntry? room = await roomResponse.Content.ReadFromJsonAsync<RoomCatalogEntry>();
            Assert.NotNull(room);
            roomId = room.Id;
        }

        // 可读取目录并看到该房间。
        using (HttpRequestMessage listRooms = new(HttpMethod.Get, $"/api/workspaces/{WorkspaceId}/rooms"))
        {
            listRooms.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage catalogResponse = await client.SendAsync(listRooms, CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
            RoomCatalogEntry[] rooms = await catalogResponse.Content
                .ReadFromJsonAsync<RoomCatalogEntry[]>() ?? [];
            Assert.Contains(rooms, item => item.Id == roomId);
        }

        // 可用开发身份创建预约（ OrganizerId 必须等于令牌 sub ）。
        using (HttpRequestMessage createBooking = new(
                   HttpMethod.Post,
                   $"/api/workspaces/{WorkspaceId}/bookings"))
        {
            createBooking.Content = JsonContent.Create(new CreateBookingRequest(
                roomId,
                login.UserId,
                "Dev booking",
                new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
                "UTC"));
            createBooking.Headers.Add("Idempotency-Key", $"dev-token-booking-{roomId}");
            createBooking.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            using HttpResponseMessage bookingResponse = await client.SendAsync(createBooking, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
        }
    }

    [Fact]
    public async Task SameNameAlwaysMapsToSameUserIdAndDifferentNamesDiffer()
    {
        using var factory = new DevTokenServerFactory(enabled: true);
        using HttpClient client = factory.CreateClient();

        DevTokenLoginResponse first = await LoginAsync(client, "张三");
        DevTokenLoginResponse second = await LoginAsync(client, "张三");
        DevTokenLoginResponse other = await LoginAsync(client, "李四");

        Assert.Equal(first.UserId, second.UserId);
        Assert.NotEqual(first.UserId, other.UserId);
    }

    [Fact]
    public async Task BlankNameIsRejected()
    {
        using var factory = new DevTokenServerFactory(enabled: true);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/dev-token",
            new { name = "   " },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SharedSecretIsEnforcedWhenConfigured()
    {
        using var factory = new DevTokenServerFactory(enabled: true, sharedSecret: "team-passphrase-2026");
        using HttpClient client = factory.CreateClient();

        using (HttpRequestMessage missing = new(HttpMethod.Post, "/api/auth/dev-token"))
        {
            missing.Content = JsonContent.Create(new { name = "张三" });
            using HttpResponseMessage response = await client.SendAsync(missing, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using (HttpRequestMessage wrong = new(HttpMethod.Post, "/api/auth/dev-token"))
        {
            wrong.Content = JsonContent.Create(new { name = "张三", sharedSecret = "wrong-passphrase" });
            using HttpResponseMessage response = await client.SendAsync(wrong, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using HttpResponseMessage accepted = await client.PostAsJsonAsync(
            "/api/auth/dev-token",
            new { name = "张三", sharedSecret = "team-passphrase-2026" },
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        DevTokenLoginResponse? login = await accepted.Content.ReadFromJsonAsync<DevTokenLoginResponse>();
        Assert.NotNull(login);
        Assert.NotEmpty(login!.AccessToken);
    }

    [Fact]
    public async Task SharedSecretIsOptionalWhenNotConfigured()
    {
        using var factory = new DevTokenServerFactory(enabled: true);
        using HttpClient client = factory.CreateClient();

        DevTokenLoginResponse login = await LoginAsync(client, "张三");

        Assert.NotEmpty(login.AccessToken);
    }

    private static async Task<DevTokenLoginResponse> LoginAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/dev-token",
            new { name },
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DevTokenLoginResponse? login = await response.Content.ReadFromJsonAsync<DevTokenLoginResponse>();
        Assert.NotNull(login);
        return login;
    }

    private sealed record DevTokenLoginResponse(
        string AccessToken,
        string TokenType,
        Guid UserId,
        Guid WorkspaceId,
        DateTimeOffset ExpiresAtUtc);

    private sealed class DevTokenServerFactory(bool enabled, string? sharedSecret = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (enabled)
            {
                builder.UseSetting("Authentication:DevToken:Enabled", "true");
                builder.UseSetting("Authentication:DevToken:SigningKey", SigningKey);
                builder.UseSetting("Authentication:DevToken:WorkspaceId", WorkspaceId.ToString());
                if (sharedSecret is not null)
                {
                    builder.UseSetting("Authentication:DevToken:SharedSecret", sharedSecret);
                }
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IWorkspaceMembershipStore>(new InMemoryWorkspaceMembershipStore());
                services.AddSingleton<IRoomBookingStore, InMemoryRoomBookingStore>();
                services.AddSingleton<IRoomCatalogStore, InMemoryRoomCatalogStore>();
                services.AddSingleton<IChangeNotifier, NoOpChangeNotifier>();
            });
        }
    }
}
