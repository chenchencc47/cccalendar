using CcCalendar.Core.Tools;
using CcCalendar.Desktop.Tools;

namespace CcCalendar.Desktop.Tests.Tools;

public sealed class ClipboardCaptureCoordinatorTests
{
    [Fact]
    public async Task TextImageAndFilesAreRoutedToHistory()
    {
        var reader = new QueueClipboardContentReader(
        [
            ClipboardContent.FromText("Release checklist"),
            ClipboardContent.FromImage([137, 80, 78, 71]),
            ClipboardContent.FromFiles([@"C:\work\release-notes.md"]),
        ]);
        var history = new RecordingClipboardHistoryService();
        var coordinator = new ClipboardCaptureCoordinator(reader, history);

        await coordinator.CaptureCurrentAsync(CancellationToken.None);
        await coordinator.CaptureCurrentAsync(CancellationToken.None);
        await coordinator.CaptureCurrentAsync(CancellationToken.None);

        Assert.Equal(["Release checklist"], history.Texts);
        Assert.Equal([4], history.ImageLengths);
        Assert.Equal([@"C:\work\release-notes.md"], Assert.Single(history.FileLists));
    }

    private sealed class QueueClipboardContentReader(
        Queue<ClipboardContent> contents) : IClipboardContentReader
    {
        public QueueClipboardContentReader(IEnumerable<ClipboardContent> contents)
            : this(new Queue<ClipboardContent>(contents))
        {
        }

        public ClipboardContent? Read() => contents.Dequeue();
    }

    private sealed class RecordingClipboardHistoryService : IClipboardHistoryService
    {
        public List<string> Texts { get; } = [];

        public List<int> ImageLengths { get; } = [];

        public List<IReadOnlyList<string>> FileLists { get; } = [];

        public Task<ClipboardHistoryEntry> AddTextAsync(
            string text,
            CancellationToken cancellationToken)
        {
            Texts.Add(text);
            return Task.FromResult(ClipboardHistoryEntry.CreateText(text, DateTimeOffset.UtcNow));
        }

        public Task<ClipboardHistoryEntry> AddImageAsync(
            byte[] imageBytes,
            CancellationToken cancellationToken)
        {
            ImageLengths.Add(imageBytes.Length);
            return Task.FromResult(ClipboardHistoryEntry.CreateImage("image.png", DateTimeOffset.UtcNow));
        }

        public Task<ClipboardHistoryEntry> AddFilesAsync(
            IReadOnlyList<string> filePaths,
            CancellationToken cancellationToken)
        {
            FileLists.Add(filePaths);
            return Task.FromResult(ClipboardHistoryEntry.CreateFiles(filePaths, DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<ClipboardHistoryEntry>> SearchAsync(
            string query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ClipboardPayload> GetPayloadAsync(
            Guid id,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
