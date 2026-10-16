using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.AI;

/// <summary>
/// 本地代理工具目录（P71-01）：read_file/list_directory/search_files（只读三件套，
/// 默认允许）与 write_file/run_command（写入两件套，需审批）。
/// P71-03 增 spawn_subagent：同 provider/model 子会话的委派工具，按深度注入。
/// </summary>
public sealed class LocalAgentAiToolCatalog
{
    public const string ReadFileToolName = "read_file";
    public const string ListDirectoryToolName = "list_directory";
    public const string SearchFilesToolName = "search_files";
    public const string WriteFileToolName = "write_file";
    public const string RunCommandToolName = "run_command";
    public const string SpawnSubagentToolName = "spawn_subagent";

    /// <summary>
    /// 子代理嵌套上限（P71-03）：主会话 depth=0 可 spawn（depth=1），depth=1 仍可
    /// spawn（depth=2），depth=2 的目录不再注入 spawn——即主会话之外最多 2 层。
    /// </summary>
    public const int MaximumSubagentDepth = 2;

    /// <summary>
    /// spawn_subagent 工具定义（P71-03）：单定义静态属性——不属于本地五件套
    /// （不走 LocalAgentToolExecutor），由 ResolveTools 按深度决定是否注入。
    /// </summary>
    public static AiToolDefinition SpawnSubagentTool { get; } = new(
        SpawnSubagentToolName,
        "生成一个子代理执行独立子任务：同一模型开子会话并返回其最终答复。"
            + "子代理可使用当前会话的可用工具，但嵌套超过上限后不可再委派。"
            + "适合可并行拆解的调查、汇总类任务。",
        """{"type":"object","required":["description","task"],"properties":{"description":{"type":"string","description":"子任务的一句话描述，会显示在会话中"},"task":{"type":"string","description":"交给子代理完整执行的任务指令"}}}""",
        false);

    public IReadOnlyList<AiToolDefinition> Definitions { get; } =
    [
        new(
            ReadFileToolName,
            "读取本地文本文件内容。超过大小上限时截断并在结果中标注。",
            """{"type":"object","required":["path"],"properties":{"path":{"type":"string","description":"文件的绝对路径"}}}""",
            true),
        new(
            ListDirectoryToolName,
            "列出本地目录的直接子项（名称、类型、大小、修改时间）。",
            """{"type":"object","required":["path"],"properties":{"path":{"type":"string","description":"目录的绝对路径"}}}""",
            true),
        new(
            SearchFilesToolName,
            "按文件名通配符递归搜索文件，返回匹配的相对路径列表。",
            """{"type":"object","required":["pattern"],"properties":{"pattern":{"type":"string","description":"文件名通配符，如 *.md 或 report*"},"root":{"type":"string","description":"搜索根目录的绝对路径，默认当前工作目录"}}}""",
            true),
        new(
            WriteFileToolName,
            "将文本内容写入本地文件（UTF-8，父目录不存在时自动创建）。覆盖已有文件。",
            """{"type":"object","required":["path","content"],"properties":{"path":{"type":"string","description":"目标文件的绝对路径"},"content":{"type":"string","description":"要写入的完整文本内容"}}}""",
            false),
        new(
            RunCommandToolName,
            "在本地 shell 中执行命令并返回退出码与输出。命令有超时限制。",
            """{"type":"object","required":["command"],"properties":{"command":{"type":"string","description":"要执行的命令行"},"cwd":{"type":["string","null"],"description":"工作目录的绝对路径，省略时用默认工作目录"}}}""",
            false),
    ];

    /// <summary>按设置过滤出允许进入本轮工具目录的本地工具。</summary>
    public IReadOnlyList<AiToolDefinition> GetAllowedTools(LocalAgentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return [.. Definitions.Where(definition => IsAllowed(settings, definition.Name))];
    }

    /// <summary>判断工具名是否属于本地代理工具集（含被开关关掉的工具）。</summary>
    public static bool IsLocalTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return toolName is ReadFileToolName
            or ListDirectoryToolName
            or SearchFilesToolName
            or WriteFileToolName
            or RunCommandToolName;
    }

    /// <summary>
    /// 该工具执行前是否需要运行时审批（P71-02）：本地写入两件套。
    /// 只读三件套与查询/提案工具不走审批（提案自有确认流）。
    /// </summary>
    public static bool RequiresApproval(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return toolName is WriteFileToolName or RunCommandToolName;
    }

    private static bool IsAllowed(LocalAgentSettings settings, string toolName) => toolName switch
    {
        ReadFileToolName => settings.AllowReadFile,
        ListDirectoryToolName => settings.AllowListDirectory,
        SearchFilesToolName => settings.AllowSearchFiles,
        WriteFileToolName => settings.AllowWriteFile,
        RunCommandToolName => settings.AllowRunCommand,
        _ => false,
    };
}
