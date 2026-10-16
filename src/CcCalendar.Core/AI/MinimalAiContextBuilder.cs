namespace CcCalendar.Core.AI;

public sealed record AiRequestContext(
    string SystemInstruction,
    string UserMessage,
    IReadOnlyList<AiToolDefinition> Tools,
    IReadOnlyList<AiAssistantTurn> Conversation);

public static class MinimalAiContextBuilder
{
    private const string ReadOnlyInstruction =
        "你是 cccalendar 助手。本轮仅可使用提供的只读查询工具；仅在回答当前问题所需时调用工具。";
    private const string WriteInstruction =
        "你是 cccalendar 助手。本轮可以查询数据并提出写入草案。所有 propose_ 工具只生成待用户确认的预览，绝不能声称已经执行；涉及多个日期或项目时在同一轮逐项调用提案工具。日程 title 只填写会议主题，不得包含新增/创建等操作词、会议链接、会议号、地点或其他字段标签；地点填写 location，腾讯会议号填写 meetingNumber。";
    private const string LocalAgentInstruction =
        "你是 cccalendar 助手。本轮可以查询应用数据、提出写入草案，并按需使用本地文件与命令工具（read_file/list_directory/search_files/write_file/run_command）。所有 propose_ 工具只生成待用户确认的预览，绝不能声称已经执行。本地工具中只有本轮目录里列出的才可用；write_file 与 run_command 会先请求用户批准，被拒绝时应改用只读方案或如实说明。涉及文件操作时优先使用只读三件套定位目标，再申请写入。";
    private static readonly string[] WeekdayNames = ["日", "一", "二", "三", "四", "五", "六"];

    public static AiRequestContext Build(
        string userMessage,
        IReadOnlyList<AiToolDefinition> tools,
        AiAssistantMode mode = AiAssistantMode.ReadOnly,
        DateTimeOffset? localNow = null,
        IReadOnlyList<AiAssistantTurn>? conversation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentNullException.ThrowIfNull(tools);
        if (mode == AiAssistantMode.ReadOnly && tools.Any(tool => !tool.IsReadOnly))
        {
            throw new ArgumentException("Only read-only tools may enter this context.", nameof(tools));
        }

        string instruction = mode switch
        {
            AiAssistantMode.ReadOnly => ReadOnlyInstruction,
            AiAssistantMode.Write => WriteInstruction,
            AiAssistantMode.LocalAgent => LocalAgentInstruction,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        if (localNow is { } now)
        {
            instruction =
                $"{instruction}当前本地时间：{now:yyyy-MM-dd}（周{WeekdayNames[(int)now.DayOfWeek]}）{now:HH:mm}。用户提到\"今天/明天/本周\"等相对日期时，必须直接据此推算成具体日期，不要再反问用户。";
        }

        return new AiRequestContext(
            instruction,
            userMessage.Trim(),
            tools,
            conversation is null ? [] : [.. conversation.TakeLast(40)]);
    }
}
