namespace CcCalendar.Core.AI;

public sealed class ReadOnlyAiToolCatalog
{
    public IReadOnlyList<AiToolDefinition> Definitions { get; } =
    [
        new(
            "query_events",
            "按标题和时间范围查询日程。",
            """{"type":"object","properties":{"query":{"type":"string"},"fromUtc":{"type":"string","format":"date-time"},"toUtc":{"type":"string","format":"date-time"},"limit":{"type":"integer","minimum":1,"maximum":50}}}""",
            true),
        new(
            "query_projects",
            "按名称查询项目摘要。",
            """{"type":"object","properties":{"query":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":50}}}""",
            true),
        new(
            "query_records",
            "按标题或正文查询记录；正文默认不返回。",
            """{"type":"object","properties":{"query":{"type":"string"},"includeContent":{"type":"boolean"},"limit":{"type":"integer","minimum":1,"maximum":50}}}""",
            true),
        new(
            "query_todos",
            "按标题查询待办摘要。",
            """{"type":"object","properties":{"query":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":50}}}""",
            true),
    ];
}
