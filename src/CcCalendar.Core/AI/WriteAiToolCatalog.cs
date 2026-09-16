namespace CcCalendar.Core.AI;

public sealed class WriteAiToolCatalog
{
    public IReadOnlyList<AiToolDefinition> Definitions { get; } =
    [
        new(
            "propose_timed_event",
            "提出定时日程草案。此工具只生成待用户确认的预览，不直接写入。会议地点、腾讯会议号必须分别填写，不要拼进 title。startAt/endAt 使用用户所在时区的本地时间并带时区偏移（如 2026-08-24T18:10:00+08:00），不要用 Z/UTC 表示。",
            """{"type":"object","required":["title","startAt","endAt","timeZoneId"],"properties":{"title":{"type":"string"},"startAt":{"type":"string","format":"date-time","description":"用户所在时区的本地时间，带时区偏移，例如 2026-08-24T18:10:00+08:00"},"endAt":{"type":"string","format":"date-time","description":"用户所在时区的本地时间，带时区偏移，例如 2026-08-24T19:10:00+08:00"},"timeZoneId":{"type":"string","description":"用户所在时区 ID，如 Asia/Shanghai 或 China Standard Time"},"location":{"type":["string","null"]},"meetingNumber":{"type":["string","null"],"description":"腾讯会议号，例如 365-5683-5623"}}}""",
            false),
        new(
            "propose_all_day_event",
            "提出全天日程草案。结束日期不包含在日程内。",
            """{"type":"object","required":["title","startDate","endDateExclusive"],"properties":{"title":{"type":"string"},"startDate":{"type":"string","format":"date"},"endDateExclusive":{"type":"string","format":"date"}}}""",
            false),
        new(
            "propose_todo",
            "提出待办草案，截止时间可省略。",
            """{"type":"object","required":["title"],"properties":{"title":{"type":"string"},"dueAt":{"type":["string","null"],"format":"date-time"}}}""",
            false),
        new(
            "propose_record",
            "提出工作记录草案。recordType 可为 WorkLog、Meeting、RequirementChange 或 Issue。",
            """{"type":"object","required":["title","recordType","content"],"properties":{"title":{"type":"string"},"recordType":{"type":"string","enum":["WorkLog","Meeting","RequirementChange","Issue"]},"content":{"type":"string"}}}""",
            false),
        new(
            "propose_todo_status",
            "提出待办状态变更。应先查询待办获得 id。",
            """{"type":"object","required":["todoId","status"],"properties":{"todoId":{"type":"string","format":"uuid"},"status":{"type":"string","enum":["Inbox","NotStarted","InProgress","Blocked","Completed"]}}}""",
            false),
        new(
            "propose_delete",
            "提出软删除实体。应先查询获得 id；删除后可从回收站恢复。",
            """{"type":"object","required":["entityKind","entityId"],"properties":{"entityKind":{"type":"string","enum":["Project","Todo","Event","Record"]},"entityId":{"type":"string","format":"uuid"}}}""",
            false),
    ];
}
