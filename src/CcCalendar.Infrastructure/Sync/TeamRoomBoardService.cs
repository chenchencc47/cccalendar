using CcCalendar.Core.Configuration;
using CcCalendar.Core.Rooms;

namespace CcCalendar.Infrastructure.Sync;

public sealed record TeamRoomBoardData(
    IReadOnlyList<RoomCatalogEntry> Rooms,
    IReadOnlyList<RoomBookingResult> Bookings);

/// <summary>
/// 团队会议室看板服务：按天拉取团队房间目录和远程预约，提交在线预约。
/// 客户端由工厂按当前连接配置创建（服务端地址可变）。
/// </summary>
public sealed class TeamRoomBoardService(
    Func<TeamConnectionSettings> settingsProvider,
    Func<TeamConnectionSettings, IRoomBookingClient> clientFactory)
{
    private IReadOnlyList<RoomCatalogEntry> cachedRooms = [];

    public bool IsConfigured
    {
        get
        {
            TeamConnectionSettings settings = settingsProvider();
            return Uri.TryCreate(settings.ApiBaseUrl?.Trim(), UriKind.Absolute, out _)
                && Guid.TryParse(settings.WorkspaceId, out _);
        }
    }

    /// <summary>加载指定本地日期的团队房间目录与远程预约；未配置团队连接时返回 null。</summary>
    public async Task<TeamRoomBoardData?> LoadAsync(
        DateOnly date,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        (Guid? workspaceId, IRoomBookingClient? client) = CreateClient();
        if (workspaceId is null || client is null)
        {
            return null;
        }

        DateTimeOffset fromUtc = TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(TimeOnly.MinValue),
            timeZone);
        DateTimeOffset toUtc = fromUtc.AddDays(1);
        IReadOnlyList<RoomCatalogEntry> rooms = await client.GetRoomsAsync(
            workspaceId.Value,
            cancellationToken);
        IReadOnlyList<RoomBookingResult> bookings = await client.GetBookingsAsync(
            workspaceId.Value,
            fromUtc,
            toUtc,
            cancellationToken);
        cachedRooms = rooms;
        return new TeamRoomBoardData(rooms, bookings);
    }

    /// <summary>
    /// 提交团队预约。未配置连接、缺少当前用户 ID 或房间不在团队目录时返回 false（调用方静默跳过）；
    /// 服务端拒绝（如 409 冲突）会抛出 HttpRequestException，由调用方提示。
    /// </summary>
    public async Task<bool> TryCreateBookingAsync(
        string roomName,
        string title,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken,
        string? idempotencyKey = null,
        string? meetingInvitationText = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(timeZone);
        (Guid? workspaceId, IRoomBookingClient? client) = CreateClient();
        if (workspaceId is null || client is null)
        {
            return false;
        }

        TeamConnectionSettings settings = settingsProvider();
        if (!Guid.TryParse(settings.CurrentUserId, out Guid organizerId))
        {
            return false;
        }

        if (cachedRooms.Count == 0)
        {
            cachedRooms = await client.GetRoomsAsync(workspaceId.Value, cancellationToken);
        }

        RoomCatalogEntry? room = cachedRooms.FirstOrDefault(
            item => string.Equals(item.Name, roomName.Trim(), StringComparison.Ordinal));
        if (room is null)
        {
            return false;
        }

        DateTimeOffset startUtc = TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(start),
            timeZone);
        DateTimeOffset endUtc = TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(end),
            timeZone);
        await client.CreateBookingAsync(
            workspaceId.Value,
            new RoomBookingRequest(
                room.Id,
                organizerId,
                title,
                startUtc,
                endUtc,
                timeZone.Id,
                meetingInvitationText),
            string.IsNullOrWhiteSpace(idempotencyKey)
                ? Guid.NewGuid().ToString("N")
                : idempotencyKey.Trim(),
            cancellationToken);
        return true;
    }

    public async Task<bool> TryUpdateBookingInvitationAsync(
        Guid bookingId,
        string meetingInvitationText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingInvitationText);
        (Guid? workspaceId, IRoomBookingClient? client) = CreateClient();
        if (workspaceId is null || client is null)
        {
            return false;
        }

        await client.UpdateBookingInvitationAsync(
            bookingId,
            meetingInvitationText.Trim(),
            cancellationToken);
        return true;
    }

    private (Guid? WorkspaceId, IRoomBookingClient? Client) CreateClient()
    {
        TeamConnectionSettings settings = settingsProvider();
        if (!Uri.TryCreate(settings.ApiBaseUrl?.Trim(), UriKind.Absolute, out _)
            || !Guid.TryParse(settings.WorkspaceId, out Guid workspaceId))
        {
            return (null, null);
        }

        return (workspaceId, clientFactory(settings));
    }
}
