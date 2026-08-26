using System.Net.Http;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Rooms;
using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Infrastructure.Tests.Sync;

public sealed class TeamRoomBoardServiceTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    [Fact]
    public async Task LoadReturnsNullWhenTeamConnectionIsNotConfigured()
    {
        var service = new TeamRoomBoardService(
            () => new TeamConnectionSettings(),
            _ => throw new InvalidOperationException("不应创建客户端。"));

        TeamRoomBoardData? data = await service.LoadAsync(
            new DateOnly(2026, 8, 24),
            TimeZoneInfo.Utc,
            CancellationToken.None);

        Assert.Null(data);
    }

    [Fact]
    public async Task LoadFetchesRoomsAndBookingsCoveringTheLocalDay()
    {
        var client = new FakeBookingClient();
        var service = CreateService(client);

        TeamRoomBoardData? data = await service.LoadAsync(
            new DateOnly(2026, 8, 24),
            Zone,
            CancellationToken.None);

        Assert.NotNull(data);
        Assert.Single(data.Rooms);
        Assert.Equal(RoomId, data.Rooms[0].Id);
        Assert.Single(data.Bookings);
        // 2026-08-24 00:00 +08:00 = 2026-08-23 16:00Z；次日 00:00 = 2026-08-24 16:00Z。
        Assert.Equal(
            new DateTimeOffset(2026, 8, 23, 16, 0, 0, TimeSpan.Zero),
            client.LastFromUtc);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero),
            client.LastToUtc);
    }

    [Fact]
    public async Task TryCreateBookingSendsRoomIdOrganizerAndUtcTimes()
    {
        var client = new FakeBookingClient();
        var service = CreateService(client);

        bool created = await service.TryCreateBookingAsync(
            "大会议室",
            "周会",
            new DateOnly(2026, 8, 24),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            Zone,
            CancellationToken.None);

        Assert.True(created);
        Assert.NotNull(client.LastCreateRequest);
        Assert.Equal(RoomId, client.LastCreateRequest!.RoomId);
        Assert.Equal(UserId, client.LastCreateRequest.OrganizerId);
        Assert.Equal("周会", client.LastCreateRequest.Title);
        // 2026-08-24 09:00 +08:00 = 2026-08-24 01:00Z。
        Assert.Equal(
            new DateTimeOffset(2026, 8, 24, 1, 0, 0, TimeSpan.Zero),
            client.LastCreateRequest.StartAt);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
            client.LastCreateRequest.EndAt);
        Assert.False(string.IsNullOrWhiteSpace(client.LastIdempotencyKey));
    }

    [Fact]
    public async Task TryCreateBookingUsesStableIdempotencyKeyWhenProvided()
    {
        var client = new FakeBookingClient();
        var service = CreateService(client);

        bool created = await service.TryCreateBookingAsync(
            "大会议室",
            "每日例会",
            new DateOnly(2026, 8, 24),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            Zone,
            CancellationToken.None,
            "calendar-event-stable");

        Assert.True(created);
        Assert.Equal("calendar-event-stable", client.LastIdempotencyKey);
    }

    [Fact]
    public async Task TryCreateBookingReturnsFalseWhenRoomUnknownOrUserMissing()
    {
        var client = new FakeBookingClient();
        var service = CreateService(client, currentUserId: string.Empty);

        bool withoutUser = await service.TryCreateBookingAsync(
            "大会议室",
            "周会",
            new DateOnly(2026, 8, 24),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            Zone,
            CancellationToken.None);
        Assert.False(withoutUser);
        Assert.Null(client.LastCreateRequest);

        var knownRoomService = CreateService(client);
        bool unknownRoom = await knownRoomService.TryCreateBookingAsync(
            "不存在的房间",
            "周会",
            new DateOnly(2026, 8, 24),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            Zone,
            CancellationToken.None);
        Assert.False(unknownRoom);
        Assert.Null(client.LastCreateRequest);
    }

    private static TeamRoomBoardService CreateService(
        FakeBookingClient client,
        string currentUserId = "set")
    {
        var settings = new TeamConnectionSettings
        {
            ApiBaseUrl = "http://192.168.1.88:5080/",
            WorkspaceId = WorkspaceId.ToString(),
            CurrentUserId = currentUserId == "set" ? UserId.ToString() : currentUserId,
        };
        return new TeamRoomBoardService(() => settings, _ => client);
    }

    private sealed class FakeBookingClient : IRoomBookingClient
    {
        public DateTimeOffset? LastFromUtc { get; private set; }

        public DateTimeOffset? LastToUtc { get; private set; }

        public RoomBookingRequest? LastCreateRequest { get; private set; }

        public string? LastIdempotencyKey { get; private set; }

        public Task<IReadOnlyList<RoomCatalogEntry>> GetRoomsAsync(
            Guid workspaceId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RoomCatalogEntry>>(
            [
                new RoomCatalogEntry(RoomId, workspaceId, "大会议室", "Asia/Shanghai", true),
            ]);
        }

        public Task<IReadOnlyList<RoomBookingResult>> GetBookingsAsync(
            Guid workspaceId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            LastFromUtc = fromUtc;
            LastToUtc = toUtc;
            return Task.FromResult<IReadOnlyList<RoomBookingResult>>(
            [
                new RoomBookingResult(
                    Guid.NewGuid(),
                    workspaceId,
                    RoomId,
                    Guid.NewGuid(),
                    "占用",
                    new DateTimeOffset(2026, 8, 24, 2, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 24, 3, 0, 0, TimeSpan.Zero),
                    "UTC"),
            ]);
        }

        public Task<RoomBookingResult> CreateBookingAsync(
            Guid workspaceId,
            RoomBookingRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            LastCreateRequest = request;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(new RoomBookingResult(
                Guid.NewGuid(),
                workspaceId,
                request.RoomId,
                request.OrganizerId,
                request.Title,
                request.StartAt,
                request.EndAt,
                request.TimeZoneId));
        }

        public Task CancelBookingAsync(Guid bookingId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpdateBookingInvitationAsync(
            Guid bookingId,
            string meetingInvitationText,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
