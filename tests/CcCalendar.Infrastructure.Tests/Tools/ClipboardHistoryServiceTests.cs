using CcCalendar.Core.Tools;
using CcCalendar.Infrastructure.Tests.Persistence;
using CcCalendar.Infrastructure.Tools;

namespace CcCalendar.Infrastructure.Tests.Tools;

public sealed class ClipboardHistoryServiceTests : IAsyncDisposable
{
    private readonly string storageDirectory = Path.Combine(
        Path.GetTempPath(),
        "cccalendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TextImageAndFilesPersistSearchAndFavorite()
    {
        await using var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        var service = new ClipboardHistoryService(
            database.DatabasePath,
            storageDirectory,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)));

        ClipboardHistoryEntry text = await service.AddTextAsync(
            "Release checklist",
            CancellationToken.None);
        ClipboardHistoryEntry image = await service.AddImageAsync(
            [137, 80, 78, 71],
            CancellationToken.None);
        ClipboardHistoryEntry files = await service.AddFilesAsync(
            [@"C:\work\release-notes.md", @"C:\work\build.zip"],
            CancellationToken.None);
        await service.ToggleFavoriteAsync(text.Id, CancellationToken.None);

        IReadOnlyList<ClipboardHistoryEntry> matches = await service.SearchAsync(
            "release",
            CancellationToken.None);
        ClipboardPayload imagePayload = await service.GetPayloadAsync(image.Id, CancellationToken.None);

        Assert.Equal(2, matches.Count);
        Assert.Equal(text.Id, matches[0].Id);
        Assert.True(matches[0].IsFavorite);
        Assert.Contains(matches, item => item.Id == files.Id);
        Assert.Equal(ClipboardItemKind.Image, image.Kind);
        Assert.True(File.Exists(imagePayload.ImagePath));
        Assert.Equal(3, (await service.SearchAsync(string.Empty, CancellationToken.None)).Count);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(storageDirectory))
        {
            Directory.Delete(storageDirectory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
