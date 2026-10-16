using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CcCalendar.Core.AI;

namespace CcCalendar.Infrastructure.AI;

/// <summary>
/// 本地代理工具执行器（P71-01）：read_file/list_directory/search_files/write_file/
/// run_command 的实际执行。查询/读取类把"文件不存在"这类可预期失败以 {"error":...}
/// JSON 返回给模型自纠（不中断整轮）；真正的误用（未知工具名）才抛异常。
/// </summary>
public static class LocalAgentToolExecutor
{
    private const int MaxReadCharacters = 262_144; // 读取上限 256K 字符
    private const int MaxDirectoryEntries = 200;
    private const int MaxSearchResults = 100;
    private const int MaxCommandOutputCharacters = 65_536;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Task<AiToolExecutionResult> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return toolName switch
        {
            LocalAgentAiToolCatalog.ReadFileToolName => ReadFileAsync(arguments, cancellationToken),
            LocalAgentAiToolCatalog.ListDirectoryToolName => ListDirectoryAsync(
                arguments, cancellationToken),
            LocalAgentAiToolCatalog.SearchFilesToolName => SearchFilesAsync(
                arguments, cancellationToken),
            LocalAgentAiToolCatalog.WriteFileToolName => WriteFileAsync(
                arguments, cancellationToken),
            LocalAgentAiToolCatalog.RunCommandToolName => RunCommandAsync(
                arguments, cancellationToken),
            _ => throw new InvalidOperationException($"Tool '{toolName}' is not allowed."),
        };
    }

    private static async Task<AiToolExecutionResult> ReadFileAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string path = ReadRequiredString(arguments, "path");
        if (!File.Exists(path))
        {
            return Error($"文件不存在：{path}");
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        bool truncated = content.Length > MaxReadCharacters;
        var payload = new Dictionary<string, object?>
        {
            ["path"] = path,
            ["content"] = truncated ? content[..MaxReadCharacters] : content,
            ["truncated"] = truncated,
        };
        return Ok(payload);
    }

    private static Task<AiToolExecutionResult> ListDirectoryAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string path = ReadRequiredString(arguments, "path");
        if (!Directory.Exists(path))
        {
            return Task.FromResult(Error($"目录不存在：{path}"));
        }

        var entries = new List<Dictionary<string, object?>>();
        foreach (FileSystemInfo info in new DirectoryInfo(path)
            .EnumerateFileSystemInfos()
            .Take(MaxDirectoryEntries))
        {
            entries.Add(new Dictionary<string, object?>
            {
                ["name"] = info.Name,
                ["kind"] = info is DirectoryInfo ? "directory" : "file",
                ["length"] = info is FileInfo file ? file.Length : null,
                ["lastWriteTime"] = info.LastWriteTime.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture),
            });
        }

        return Task.FromResult(Ok(new Dictionary<string, object?>
        {
            ["path"] = path,
            ["entries"] = entries,
            ["truncated"] = entries.Count == MaxDirectoryEntries,
        }));
    }

    private static Task<AiToolExecutionResult> SearchFilesAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string pattern = ReadRequiredString(arguments, "pattern");
        string root = arguments.TryGetProperty("root", out JsonElement rootNode)
            && rootNode.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(rootNode.GetString())
                ? rootNode.GetString()!
                : Environment.CurrentDirectory;
        if (!Directory.Exists(root))
        {
            return Task.FromResult(Error($"目录不存在：{root}"));
        }

        var matches = new List<string>();
        try
        {
            CollectMatches(root, root, pattern, matches, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // 无权限子树跳过，已有匹配仍可用。
        }

        return Task.FromResult(Ok(new Dictionary<string, object?>
        {
            ["root"] = root,
            ["pattern"] = pattern,
            ["matches"] = matches,
            ["truncated"] = matches.Count == MaxSearchResults,
        }));
    }

    private static void CollectMatches(
        string root,
        string directory,
        string pattern,
        List<string> matches,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (string file in Directory.EnumerateFiles(directory, pattern))
        {
            matches.Add(Path.GetRelativePath(root, file));
            if (matches.Count >= MaxSearchResults)
            {
                return;
            }
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (matches.Count >= MaxSearchResults)
            {
                return;
            }

            try
            {
                CollectMatches(root, child, pattern, matches, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // 跳过无权限子目录。
            }
        }
    }

    private static async Task<AiToolExecutionResult> WriteFileAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string path = ReadRequiredString(arguments, "path");
        string content = ReadRequiredString(arguments, "content");
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken);
        return Ok(new Dictionary<string, object?>
        {
            ["path"] = path,
            ["bytes"] = Encoding.UTF8.GetByteCount(content),
        });
    }

    private static async Task<AiToolExecutionResult> RunCommandAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        string command = ReadRequiredString(arguments, "command");
        string? cwd = arguments.TryGetProperty("cwd", out JsonElement cwdNode)
            && cwdNode.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(cwdNode.GetString())
                ? cwdNode.GetString()
                : null;
        if (cwd is not null && !Directory.Exists(cwd))
        {
            return Error($"工作目录不存在：{cwd}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c {command}",
            WorkingDirectory = cwd ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = OperatingSystem.IsWindows()
                ? Encoding.UTF8
                : null,
            StandardErrorEncoding = OperatingSystem.IsWindows()
                ? Encoding.UTF8
                : null,
        };
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string stdout;
        string stderr;
        using CancellationTokenSource timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            stdout = Truncate(await stdoutTask);
            stderr = Truncate(await stderrTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcess(process);
            return Error($"命令超时（超过 {CommandTimeout.TotalSeconds:0} 秒）已终止：{command}");
        }

        return Ok(new Dictionary<string, object?>
        {
            ["command"] = command,
            ["exitCode"] = process.ExitCode,
            ["stdout"] = Truncate(stdout),
            ["stderr"] = Truncate(stderr),
        });
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // 进程恰好已退出。
        }
    }

    private static string Truncate(string text) => text.Length <= MaxCommandOutputCharacters
        ? text
        : text[..MaxCommandOutputCharacters] + "…";

    private static AiToolExecutionResult Ok(Dictionary<string, object?> payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions));

    private static AiToolExecutionResult Error(string message) =>
        new(JsonSerializer.Serialize(new Dictionary<string, object?> { ["error"] = message }, JsonOptions));

    private static string ReadRequiredString(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"工具参数缺少 {propertyName}。");
        }

        return value.GetString()!;
    }
}
