using System.Text.Json;
using CcCalendar.Core.AI;
using CcCalendar.Infrastructure.AI;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class LocalAgentToolExecutorTests : IDisposable
{
    private readonly string root;

    public LocalAgentToolExecutorTests()
    {
        root = Path.Combine(Path.GetTempPath(), "cccalendar-latest-agent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileReturnsContent()
    {
        string path = Path.Combine(root, "notes.md");
        await File.WriteAllTextAsync(path, "你好，本地代理。");
        AiToolExecutionResult result = await LocalAgentToolExecutor.ExecuteAsync(
            "read_file",
            Parse($$"""{"path":{{Json(path)}}}"""),
            CancellationToken.None);

        Assert.Contains("你好，本地代理。", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("error", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadFileReturnsErrorJsonForMissingFile()
    {
        AiToolExecutionResult result = await LocalAgentToolExecutor.ExecuteAsync(
            "read_file",
            Parse($$"""{"path":{{Json(Path.Combine(root, "missing.txt"))}}}"""),
            CancellationToken.None);

        Assert.Contains("文件不存在", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListDirectoryReportsEntriesWithKind()
    {
        string sub = Path.Combine(root, "sub");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "a");
        AiToolExecutionResult result = await LocalAgentToolExecutor.ExecuteAsync(
            "list_directory",
            Parse($$"""{"path":{{Json(root)}}}"""),
            CancellationToken.None);

        Assert.Contains("a.txt", result.Json, StringComparison.Ordinal);
        Assert.Contains("sub", result.Json, StringComparison.Ordinal);
        Assert.Contains("file", result.Json, StringComparison.Ordinal);
        Assert.Contains("directory", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchFilesFindsNestedMatches()
    {
        string nested = Path.Combine(root, "docs", "deep");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "report-2026.md"), "# report");
        await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "notes");
        AiToolExecutionResult result = await LocalAgentToolExecutor.ExecuteAsync(
            "search_files",
            Parse($$"""{"pattern":"report*","root":{{Json(root)}}}"""),
            CancellationToken.None);

        Assert.Contains("report-2026.md", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("notes.md", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteFileCreatesParentDirectories()
    {
        string path = Path.Combine(root, "gen", "nested", "out.txt");
        AiToolExecutionResult result = await LocalAgentToolExecutor.ExecuteAsync(
            "write_file",
            Parse($$"""{"path":{{Json(path)}},"content":"hello agent"}"""),
            CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal("hello agent", await File.ReadAllTextAsync(path));
        Assert.Contains("bytes", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunCommandReturnsExitCodeAndOutput()
    {
        AiToolExecutionResult result = await LocalAgentToolExecutor.ExecuteAsync(
            "run_command",
            Parse($$"""{"command":"echo agent-ok"}"""),
            CancellationToken.None);

        Assert.Contains("agent-ok", result.Json, StringComparison.Ordinal);
        Assert.Contains("exitCode", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunCommandReportsMissingWorkingDirectory()
    {
        AiToolExecutionResult result = await LocalAgentToolExecutor.ExecuteAsync(
            "run_command",
            Parse($$"""{"command":"echo x","cwd":{{Json(Path.Combine(root, "no-such-dir"))}}}"""),
            CancellationToken.None);

        Assert.Contains("工作目录不存在", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownToolThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => LocalAgentToolExecutor.ExecuteAsync(
            "query_events",
            Parse("{}"),
            CancellationToken.None));
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string Json(string value) => JsonSerializer.Serialize(value);
}
