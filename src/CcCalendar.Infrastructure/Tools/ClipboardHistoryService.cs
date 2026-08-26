using CcCalendar.Core.Tools;
using CcCalendar.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CcCalendar.Infrastructure.Tools;

public sealed class ClipboardHistoryService : IClipboardHistoryService
{
    private readonly string connectionString;
    private readonly string storageDirectory;
    private readonly TimeProvider timeProvider;

    public ClipboardHistoryService(
        string databasePath,
        string storageDirectory,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Pooling = false,
        }.ToString();
        this.storageDirectory = Path.GetFullPath(storageDirectory);
    }

    public async Task<ClipboardHistoryEntry> AddTextAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ClipboardHistoryEntry entry = ClipboardHistoryEntry.CreateText(
            text,
            timeProvider.GetUtcNow());
        return await AddAsync(entry, cancellationToken);
    }

    public async Task<ClipboardHistoryEntry> AddImageAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image content cannot be empty.", nameof(imageBytes));
        }

        Directory.CreateDirectory(storageDirectory);
        string relativePath = $"{Guid.NewGuid():N}.png";
        string imagePath = ResolveStoragePath(relativePath);
        await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken);
        try
        {
            ClipboardHistoryEntry entry = ClipboardHistoryEntry.CreateImage(
                relativePath,
                timeProvider.GetUtcNow());
            return await AddAsync(entry, cancellationToken);
        }
        catch
        {
            File.Delete(imagePath);
            throw;
        }
    }

    public async Task<ClipboardHistoryEntry> AddFilesAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken)
    {
        ClipboardHistoryEntry entry = ClipboardHistoryEntry.CreateFiles(
            filePaths,
            timeProvider.GetUtcNow());
        return await AddAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardHistoryEntry>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        query ??= string.Empty;
        await using CalendarDbContext context = CreateContext();
        IQueryable<ClipboardHistoryEntry> entries = context.ClipboardHistoryEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalized = query.Trim().ToUpperInvariant();
            entries = entries.Where(entry => entry.SearchText.Contains(normalized));
        }

        return await entries
            .OrderByDescending(entry => entry.IsFavorite)
            .ThenByDescending(entry => entry.CreatedAtUtc)
            .ThenBy(entry => entry.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        ClipboardHistoryEntry entry = await context.ClipboardHistoryEntries
            .SingleAsync(candidate => candidate.Id == id, cancellationToken);
        entry.ToggleFavorite();
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClipboardPayload> GetPayloadAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        ClipboardHistoryEntry entry = await context.ClipboardHistoryEntries
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == id, cancellationToken);
        string[] filePaths = string.IsNullOrEmpty(entry.FilePaths)
            ? []
            : entry.FilePaths.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return new ClipboardPayload(
            entry.TextContent,
            entry.ImageRelativePath is null ? null : ResolveStoragePath(entry.ImageRelativePath),
            filePaths);
    }

    private async Task<ClipboardHistoryEntry> AddAsync(
        ClipboardHistoryEntry entry,
        CancellationToken cancellationToken)
    {
        await using CalendarDbContext context = CreateContext();
        context.ClipboardHistoryEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    private string ResolveStoragePath(string relativePath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(storageDirectory, relativePath));
        string prefix = storageDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? storageDirectory
            : storageDirectory + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Clipboard image path is outside managed storage.");
        }

        return fullPath;
    }

    private CalendarDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CalendarDbContext(options);
    }
}
