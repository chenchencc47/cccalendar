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

        string instruction = mode == AiAssistantMode.ReadOnly ? ReadOnlyInstruction : WriteInstruction;
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
