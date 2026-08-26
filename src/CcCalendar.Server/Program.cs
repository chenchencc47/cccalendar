using System.Security.Claims;
using CcCalendar.Core.Rooms;
using CcCalendar.Core.Sync;
using CcCalendar.Core.Workspaces;
using CcCalendar.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
DevTokenOptions devTokenOptions = DevTokenOptions.From(builder.Configuration);
devTokenOptions.Validate();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        if (devTokenOptions.Enabled)
        {
            // 开发令牌模式：本地对称密钥验证，且不重映射 sub 声明。
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = devTokenOptions.Issuer,
                ValidAudience = devTokenOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(devTokenOptions.SigningKey)),
            };
        }
    });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
ServerDeploymentOptions deploymentOptions = ServerDeploymentOptions.From(builder.Configuration);
deploymentOptions.Validate();
if (deploymentOptions.StorageProvider == "postgres")
{
    var postgresDatabase = new PostgresDatabase(deploymentOptions.PostgresConnectionString!);
    postgresDatabase.Initialize();
    builder.Services.AddSingleton(postgresDatabase);
    builder.Services.AddSingleton<IRoomBookingStore, PostgresRoomBookingStore>();
    builder.Services.AddSingleton<IWorkspaceMembershipStore, PostgresWorkspaceMembershipStore>();
    builder.Services.AddSingleton<IRoomCatalogStore, PostgresRoomCatalogStore>();
    builder.Services.AddSingleton<ISyncChangeStore, PostgresSyncChangeStore>();
}
else
{
    string dataDirectory = deploymentOptions.DataDirectory;
    builder.Services.AddSingleton<IRoomBookingStore>(_ => new FileRoomBookingStore(dataDirectory));
    builder.Services.AddSingleton<IWorkspaceMembershipStore, InMemoryWorkspaceMembershipStore>();
    builder.Services.AddSingleton<IRoomCatalogStore>(_ => new FileRoomCatalogStore(dataDirectory));
    builder.Services.AddSingleton<ISyncChangeStore, InMemorySyncChangeStore>();
}
builder.Services.AddSingleton<WorkspaceAuthorizationService>();
builder.Services.AddSingleton<IChangeNotifier, SignalRChangeNotifier>();

WebApplication app = builder.Build();
WorkspaceMembershipBootstrapper.Apply(
    builder.Configuration,
    app.Services.GetRequiredService<IWorkspaceMembershipStore>());
app.UseAuthentication();
app.UseAuthorization();
if (deploymentOptions.RequireHttps)
{
    app.UseHttpsRedirection();
}

app.MapHub<SyncHub>("/hubs/sync").RequireAuthorization();

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        status = "ok",
        service = "cccalendar-server",
        storage = deploymentOptions.StorageProvider,
    }));

if (devTokenOptions.Enabled)
{
    // 局域网原型专用：按姓名签发开发令牌并自动加入配置的工作区。生产环境必须关闭。
    app.MapPost(
        "/api/auth/dev-token",
        (DevTokenLoginRequest payload, IWorkspaceMembershipStore memberships) =>
        {
            string name = payload.Name?.Trim() ?? string.Empty;
            if (name.Length is < 1 or > 64)
            {
                return Results.BadRequest(new { code = "invalid_name", message = "姓名长度须为 1-64 个字符。" });
            }

            // 公网部署时配置共享口令，防止陌生人只凭姓名冒领令牌。
            if (devTokenOptions.SharedSecret.Length > 0
                && !string.Equals(payload.SharedSecret, devTokenOptions.SharedSecret, StringComparison.Ordinal))
            {
                return Results.Json(
                    new { code = "invalid_secret", message = "开发令牌口令错误。" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            Guid userId = DevTokenIssuer.GetUserId(name);
            memberships.Upsert(WorkspaceMembership.Create(
                devTokenOptions.WorkspaceId!.Value,
                userId,
                devTokenOptions.Role,
                name));
            DateTimeOffset expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(devTokenOptions.AccessTokenLifetimeMinutes);
            string accessToken = DevTokenIssuer.IssueAccessToken(devTokenOptions, userId, name, expiresAtUtc);
            return Results.Ok(new DevTokenResponse(
                accessToken,
                "Bearer",
                userId,
                devTokenOptions.WorkspaceId.Value,
                expiresAtUtc));
        });
}

app.MapGet(
    "/api/workspaces/{workspaceId:guid}/rooms",
    (Guid workspaceId,
        ClaimsPrincipal user,
        WorkspaceAuthorizationService authorization,
        IRoomCatalogStore rooms) =>
    {
        if (!authorization.Allows(user, workspaceId, WorkspacePermissions.CanReadRooms))
        {
            return Results.Forbid();
        }

        return Results.Ok(rooms.List(workspaceId).Select(RoomResponseMapper.ToResponse).ToArray());
    })
    .RequireAuthorization();

app.MapGet(
    "/api/workspaces/{workspaceId:guid}/sync",
    (Guid workspaceId,
        long cursor,
        ClaimsPrincipal user,
        WorkspaceAuthorizationService authorization,
        ISyncChangeStore changes) =>
    {
        if (!authorization.Allows(user, workspaceId, WorkspacePermissions.CanReadRooms))
        {
            return Results.Forbid();
        }

        IReadOnlyList<SyncChange> page = changes.ReadAfter(workspaceId, Math.Max(cursor, 0));
        long nextCursor = page.Count == 0 ? Math.Max(cursor, 0) : page[^1].Version;
        return Results.Ok(new SyncPageResponse(nextCursor, page));
    })
    .RequireAuthorization();

app.MapGet(
    "/api/workspaces/{workspaceId:guid}/bookings",
    (Guid workspaceId,
        string? fromUtc,
        string? toUtc,
        ClaimsPrincipal user,
        WorkspaceAuthorizationService authorization,
        IRoomBookingStore store,
        IWorkspaceMembershipStore memberships) =>
    {
        if (!authorization.Allows(user, workspaceId, WorkspacePermissions.CanReadRooms))
        {
            return Results.Forbid();
        }

        if (!DateTimeOffset.TryParse(fromUtc, out DateTimeOffset from)
            || !DateTimeOffset.TryParse(toUtc, out DateTimeOffset to)
            || to <= from)
        {
            return Results.BadRequest(new
            {
                code = "invalid_range",
                message = "fromUtc/toUtc 必须是有效时间，且 toUtc 晚于 fromUtc。",
            });
        }

        IReadOnlyList<RoomBooking> bookings = store.ListByRange(workspaceId, from, to);
        return Results.Ok(bookings.Select(booking =>
            BookingResponseMapper.ToResponse(booking, memberships)).ToArray());
    })
    .RequireAuthorization();

app.MapPost(
    "/api/workspaces/{workspaceId:guid}/rooms",
    async (Guid workspaceId,
        ClaimsPrincipal user,
        CreateRoomRequest payload,
        WorkspaceAuthorizationService authorization,
        IRoomCatalogStore rooms,
        IChangeNotifier notifier,
        ISyncChangeStore changes) =>
    {
        if (!authorization.Allows(user, workspaceId, WorkspacePermissions.CanManageRooms))
        {
            return Results.Forbid();
        }

        Room room;
        try
        {
            room = Room.Create(workspaceId, payload.Name, payload.TimeZoneId);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { code = "invalid_room", message = exception.Message });
        }

        rooms.Add(room);
        SyncChange change = changes.Append(new SyncChange(workspaceId, "room", room.Id, "created", 0));
        await notifier.PublishAsync(
            change,
            CancellationToken.None);
        return Results.Created(
            $"/api/rooms/{room.Id}",
            RoomResponseMapper.ToResponse(room));
    })
    .RequireAuthorization();

app.MapPost(
    "/api/workspaces/{workspaceId:guid}/bookings",
    async (Guid workspaceId,
        HttpRequest request,
        CreateBookingRequest payload,
        IRoomBookingStore store,
        IWorkspaceMembershipStore memberships,
        WorkspaceAuthorizationService authorization,
        IChangeNotifier notifier,
        ISyncChangeStore changes) =>
    {
        if (!authorization.Allows(
                request.HttpContext.User,
                workspaceId,
                WorkspacePermissions.CanCreateBooking))
        {
            return Results.Forbid();
        }

        string? subject = request.HttpContext.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out Guid authenticatedUserId)
            || authenticatedUserId != payload.OrganizerId)
        {
            return Results.Forbid();
        }

        string? idempotencyKey = request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.BadRequest(new { code = "idempotency_key_required" });
        }

        RoomBooking? existing = store.FindByIdempotencyKey(workspaceId, idempotencyKey);
        if (existing is not null)
        {
            return Results.Ok(BookingResponseMapper.ToResponse(
                existing,
                memberships));
        }

        RoomBooking booking;
        try
        {
            booking = RoomBooking.Create(
                workspaceId,
                payload.RoomId,
                payload.OrganizerId,
                payload.Title,
                payload.StartAt,
                payload.EndAt,
                payload.TimeZoneId,
                payload.MeetingInvitationText);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { code = "invalid_booking", message = exception.Message });
        }

        if (!store.TryAdd(booking, idempotencyKey, out IReadOnlyList<RoomBooking> conflicts))
        {
            RoomBooking? retried = store.FindByIdempotencyKey(workspaceId, idempotencyKey);
            if (retried is not null && conflicts.Count == 0)
            {
                return Results.Ok(BookingResponseMapper.ToResponse(retried, memberships));
            }

            return Results.Conflict(new BookingConflictResponse(
                "room_booking_conflict",
                [.. conflicts.Select(item => item.Id)]));
        }

        SyncChange change = changes.Append(new SyncChange(workspaceId, "room_booking", booking.Id, "created", 0));
        await notifier.PublishAsync(
            change,
            CancellationToken.None);

        return Results.Created(
            $"/api/bookings/{booking.Id}",
            BookingResponseMapper.ToResponse(booking, memberships));
    })
    .RequireAuthorization();

app.MapDelete(
    "/api/bookings/{bookingId:guid}",
    async (Guid bookingId, HttpContext httpContext, IRoomBookingStore store, ISyncChangeStore changes, IChangeNotifier notifier) =>
    {
        RoomBooking? booking = store.FindById(bookingId);
        if (booking is null)
        {
            return Results.NotFound();
        }

        string? subject = httpContext.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out Guid authenticatedUserId)
            || authenticatedUserId != booking.OrganizerId)
        {
            return Results.Forbid();
        }

        if (!store.TryDelete(bookingId))
        {
            return Results.NotFound();
        }

        SyncChange change = changes.Append(new SyncChange(
            booking.WorkspaceId,
            "room_booking",
            booking.Id,
            "deleted",
            0));
        await notifier.PublishAsync(change, CancellationToken.None);
        return Results.NoContent();
    })
    .RequireAuthorization();

app.MapPatch(
    "/api/bookings/{bookingId:guid}",
    (Guid bookingId, UpdateBookingInvitationRequest payload, HttpContext httpContext, IRoomBookingStore store) =>
    {
        RoomBooking? booking = store.FindById(bookingId);
        if (booking is null)
        {
            return Results.NotFound();
        }

        string? subject = httpContext.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out Guid authenticatedUserId)
            || authenticatedUserId != booking.OrganizerId)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(payload.MeetingInvitationText))
        {
            return Results.BadRequest(new { code = "meeting_invitation_required" });
        }

        return store.TryUpdateInvitation(bookingId, payload.MeetingInvitationText)
            ? Results.NoContent()
            : Results.NotFound();
    })
    .RequireAuthorization();

app.Run();

public partial class Program
{
}
