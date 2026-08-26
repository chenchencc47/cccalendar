using CcCalendar.Core.Tools;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class ToolCenterViewModelTests
{
    [Fact]
    public async Task CompletedPomodoroPersistsAndUpdatesTodayStatistics()
    {
        var timeProvider = new AdjustableTimeProvider(
            new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero));
        var store = new MemoryFocusSessionStore();
        var viewModel = new ToolCenterViewModel(timeProvider, store);

        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.StartOrPausePomodoro();
        timeProvider.Advance(TimeSpan.FromMinutes(25));
        await viewModel.TickAsync(CancellationToken.None);

        Assert.Single(store.Sessions);
        Assert.Equal(25, viewModel.TodayFocusMinutes);
        Assert.Equal(1, viewModel.TodayPomodoroCount);
        Assert.Equal("休息", viewModel.PomodoroPhaseText);
    }

    [Fact]
    public async Task ClipboardSearchAndFavoriteRefreshTheVisibleHistory()
    {
        DateTimeOffset now = new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        ClipboardHistoryEntry release = ClipboardHistoryEntry.CreateText("Release checklist", now);
        ClipboardHistoryEntry notes = ClipboardHistoryEntry.CreateText("Meeting notes", now.AddMinutes(1));
        var clipboardHistory = new MemoryClipboardHistoryService([release, notes]);
        var viewModel = new ToolCenterViewModel(
            new AdjustableTimeProvider(now),
            new MemoryFocusSessionStore(),
            clipboardHistoryService: clipboardHistory);

        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.ClipboardSearchQuery = "release";
        await viewModel.SearchClipboardAsync(CancellationToken.None);
        viewModel.SelectedClipboardEntry = Assert.Single(viewModel.ClipboardEntries);
        await viewModel.ToggleClipboardFavoriteAsync(CancellationToken.None);

        Assert.Equal(release.Id, viewModel.SelectedClipboardEntry?.Id);
        Assert.True(viewModel.SelectedClipboardEntry?.IsFavorite);
        Assert.True(clipboardHistory.Entries.Single(entry => entry.Id == release.Id).IsFavorite);
    }

    [Fact]
    public async Task CountdownUsesCustomDurationAndNotifiesWhenComplete()
    {
        var timeProvider = new AdjustableTimeProvider(
            new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero));
        var viewModel = new ToolCenterViewModel(timeProvider, new MemoryFocusSessionStore());
        ToolNotificationEventArgs? notification = null;
        viewModel.NotificationRequested += (_, value) => notification = value;
        viewModel.CountdownMinutes = 2;

        viewModel.StartOrPauseCountdown();
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        await viewModel.TickAsync(CancellationToken.None);

        Assert.Equal("倒计时完成", viewModel.CountdownStatus);
        Assert.Equal("倒计时完成", notification?.Title);
    }

    [Fact]
    public void FocusDurationsCanBeChangedIndependently()
    {
        var viewModel = new ToolCenterViewModel(
            new AdjustableTimeProvider(DateTimeOffset.UtcNow),
            new MemoryFocusSessionStore());

        viewModel.PomodoroFocusMinutes = 40;
        viewModel.PomodoroBreakMinutes = 15;

        Assert.Equal(40, viewModel.PomodoroFocusMinutes);
        Assert.Equal(15, viewModel.PomodoroBreakMinutes);
        Assert.Equal("40:00", viewModel.PomodoroText);
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class MemoryFocusSessionStore : IFocusSessionStore
    {
        public List<FocusSession> Sessions { get; } = [];

        public Task AddAsync(FocusSession session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FocusSession>> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<FocusSession>>(Sessions.ToArray());
        }
    }

    private sealed class MemoryClipboardHistoryService(
        IReadOnlyList<ClipboardHistoryEntry> entries) : IClipboardHistoryService
    {
        public IReadOnlyList<ClipboardHistoryEntry> Entries { get; } = entries;

        public Task<ClipboardHistoryEntry> AddTextAsync(
            string text,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClipboardHistoryEntry> AddImageAsync(
            byte[] imageBytes,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ClipboardHistoryEntry> AddFilesAsync(
            IReadOnlyList<string> filePaths,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ClipboardHistoryEntry>> SearchAsync(
            string query,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ClipboardHistoryEntry> matches = Entries
                .Where(entry => entry.Preview.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return Task.FromResult(matches);
        }

        public Task ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken)
        {
            Entries.Single(entry => entry.Id == id).ToggleFavorite();
            return Task.CompletedTask;
        }

        public Task<ClipboardPayload> GetPayloadAsync(
            Guid id,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
