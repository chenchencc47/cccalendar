namespace CcCalendar.Core.AI;

public enum AiAssistantMode
{
    ReadOnly,
    Write,

    /// <summary>
    /// 本地代理（P71-01）：Write 的超集——查询/提案工具之外，还可以使用本地文件
    /// 与命令工具（read_file/list_directory/search_files/write_file/run_command）。
    /// 默认关，需在设置页"AI 模型 → 本地代理"显式开启（Q6 裁决）。
    /// </summary>
    LocalAgent,
}

public sealed record AiAssistantTurn(bool IsUser, string Text);

public sealed record AiAssistantRequest
{
    public AiAssistantRequest(
        string userMessage,
        AiAssistantMode mode,
        IReadOnlyList<AiAssistantTurn>? conversation = null,
        Func<AiToolApprovalRequest, CancellationToken, Task<AiToolApprovalDecision>>? toolApproval = null)
    {
        UserMessage = userMessage;
        Mode = mode;
        Conversation = conversation ?? [];
        ToolApproval = toolApproval;
    }

    public string UserMessage { get; }

    public AiAssistantMode Mode { get; }

    public IReadOnlyList<AiAssistantTurn> Conversation { get; }

    /// <summary>
    /// 工具审批裁决回调（P71-02）：本地代理写入类工具（write_file/run_command）
    /// 执行前由 Infrastructure 调用，等待宿主 UI 裁决；null = 未接线，安全默认拒绝。
    /// </summary>
    public Func<AiToolApprovalRequest, CancellationToken, Task<AiToolApprovalDecision>>? ToolApproval { get; init; }

    /// <summary>
    /// 审批等待超时（P71-02）：超时默认拒绝；null = 用 Infrastructure 默认值。
    /// 测试注入短超时以覆盖超时分支。
    /// </summary>
    public TimeSpan? ApprovalTimeout { get; init; }

    /// <summary>
    /// 子代理嵌套深度（P71-03）：主会话 0，spawn_subagent 一次 +1。
    /// 上限见 <see cref="LocalAgentAiToolCatalog.MaximumSubagentDepth"/>——
    /// 达到上限的工具目录不再注入 spawn_subagent（子代理不可再委派）。
    /// </summary>
    public int SubagentDepth { get; init; }
}

/// <summary>一次工具审批请求（P71-02）：回调入参，告知宿主哪个工具在等待裁决。</summary>
public sealed record AiToolApprovalRequest(string ToolName);

/// <summary>
/// 工具审批四档裁决（P71-02，Q4 裁决全部可选）：AllowOnce 放行本次；
/// AllowForSession 本次会话内同名工具放行（宿主侧记录）；
/// AlwaysAllow 放行并写入白名单（宿主侧持久化）；Deny 拒绝——模型收到
/// error 结果可自行纠正，不视为异常。
/// </summary>
public enum AiToolApprovalDecision
{
    AllowOnce,
    AllowForSession,
    AlwaysAllow,
    Deny,
}

public abstract record AiAssistantProposal;

public sealed record AiCreationAssistantProposal(AiCreationDraft Draft) : AiAssistantProposal;

public sealed record AiChangeAssistantProposal(AiChangeSetDraft Draft) : AiAssistantProposal;

public sealed record AiAssistantResult(
    string Text,
    IReadOnlyList<AiAssistantProposal> Proposals)
{
    /// <summary>本轮 token 用量；非流式路径暂不采集，预留供调用方透传。</summary>
    public AiUsageUpdate? Usage { get; init; }
}

public abstract record AiAssistantUpdate;

public sealed record AiTextDelta(string Text) : AiAssistantUpdate;

/// <summary>
/// 一次请求的上下文构成估算（P74-02）：流开始时发一次，宿主据此渲染
/// token 构成分段条。四段为估算值（AiTokenEstimator 启发式），非 provider
/// 上报；AssistantTokens 只含会话历史回复（工具结果不进历史）。
/// </summary>
public sealed record AiContextCompositionUpdate(
    int SystemTokens,
    int ToolSchemaTokens,
    int UserTokens,
    int AssistantTokens) : AiAssistantUpdate;

/// <summary>模型返回的思考内容增量；是否返回由模型和 API 决定，不由 UI 开关控制。</summary>
public sealed record AiReasoningDelta(string Text) : AiAssistantUpdate;

public sealed record AiProposalUpdate(AiAssistantProposal Proposal) : AiAssistantUpdate;

/// <summary>
/// 工具调用的生命周期状态（P70-05）：执行中 → 成功/失败。
/// P71-02 增加 PendingApproval：写入类本地工具执行前等待用户裁决的中间态。
/// </summary>
public enum AiToolCallState
{
    PendingApproval,
    Running,
    Ok,
    Error,
}

/// <summary>
/// 一次模型工具调用的可见性事件（P70-05）：执行前发 Running，成功/失败后各发一次
/// 终态。P71-02：需审批的写入类本地工具在 Running 前先发 PendingApproval，用户
/// 拒绝时直接发 Error（ResultSummary 为拒绝原因）。ArgsSummary 是参数前几个键值
/// 拼的一行摘要（Infrastructure 生成），ResultSummary 是结果的一行摘要（大 JSON
/// 截断）；Error 时 ResultSummary 为异常消息。
/// </summary>
public sealed record AiToolCallUpdate(
    string ToolName,
    string? ArgsSummary,
    AiToolCallState State,
    string? ResultSummary = null) : AiAssistantUpdate;

/// <summary>
/// 一次模型调用（工具循环下的一个子轮）的 token 用量。缓存字段按提供商语义对齐：
/// CachedPromptTokens=缓存命中读（OpenAI prompt_tokens_details.cached_tokens /
/// DeepSeek prompt_cache_hit_tokens / Anthropic cache_read_input_tokens），
/// CacheWriteTokens=缓存写入（Anthropic cache_creation_input_tokens），
/// ReasoningTokens=推理 token；可空字段缺失表示该提供商未上报。
/// </summary>
public sealed record AiUsageUpdate(
    long PromptTokens,
    long CompletionTokens,
    long? CachedPromptTokens = null,
    long? CacheWriteTokens = null,
    long? ReasoningTokens = null,
    string? Model = null) : AiAssistantUpdate;

/// <summary>
/// 子代理生命周期开始标记（P71-03）：包在 AiSubagentUpdate.Inner 里发出，
/// 宿主据此创建子代理折叠卡。单例——无字段。
/// </summary>
public sealed record AiSubagentStarted : AiAssistantUpdate
{
    public static AiSubagentStarted Instance { get; } = new();

    private AiSubagentStarted()
    {
    }
}

/// <summary>
/// 子代理生命周期终态（P71-03）：Succeeded=false 时 Error 为一句话失败原因
/// （取消不发终态，卡片由宿主流结束兜底收束，与工具行取消语义一致）。
/// </summary>
public sealed record AiSubagentCompleted(bool Succeeded, string? Error) : AiAssistantUpdate;

/// <summary>
/// 子代理事件的包装（P71-03）：子代理子会话的每个流式事件包上子代理标识后
/// 转发到宿主流。嵌套子代理（子代理再 spawn）会形成多层包装，宿主按
/// SubagentId 逐层落卡即得 lineage 树。
/// </summary>
public sealed record AiSubagentUpdate(
    Guid SubagentId,
    string Description,
    AiAssistantUpdate Inner) : AiAssistantUpdate;
