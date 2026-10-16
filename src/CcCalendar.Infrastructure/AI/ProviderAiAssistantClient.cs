using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Records;
using CcCalendar.Core.Security;
using CcCalendar.Core.Todos;

namespace CcCalendar.Infrastructure.AI;

public sealed partial class ProviderAiAssistantClient : IAiAssistantClient
{
    private readonly Func<AiProviderSettings> getSettings;
    private readonly HttpClient httpClient;
    private readonly SqliteReadOnlyAiToolExecutor toolExecutor;
    private readonly ISecretStore secretStore;
    private readonly TimeProvider timeProvider;
    private readonly ReadOnlyAiToolCatalog toolCatalog = new();
    private readonly WriteAiToolCatalog writeToolCatalog = new();
    // P71-01：本地代理档的工具目录；getLocalAgentSettings 为 null 即未接线。
    private readonly LocalAgentAiToolCatalog localToolCatalog = new();
    private readonly Func<LocalAgentSettings>? getLocalAgentSettings;

    public ProviderAiAssistantClient(
        HttpClient httpClient,
        Func<AiProviderSettings> getSettings,
        ISecretStore secretStore,
        SqliteReadOnlyAiToolExecutor toolExecutor,
        TimeProvider? timeProvider = null,
        Func<LocalAgentSettings>? getLocalAgentSettings = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.getLocalAgentSettings = getLocalAgentSettings;
    }

    /// <summary>
    /// 按权限档解析本轮工具目录（P71-01）：ReadOnly=只读查询；Write=查询+提案；
    /// LocalAgent=查询+提案+本地工具（需设置 Enabled 且按各工具开关过滤）。
    /// P71-03：LocalAgent 且 subagentDepth 未达上限时追加 spawn_subagent——
    /// 达到上限的子会话不可再委派（第二道防线在 ExecuteSubagentAsync 深度守卫）。
    /// </summary>
    private IReadOnlyList<AiToolDefinition> ResolveTools(AiAssistantMode mode, int subagentDepth = 0)
    {
        if (mode == AiAssistantMode.LocalAgent)
        {
            LocalAgentSettings localSettings = getLocalAgentSettings?.Invoke()
                ?? throw new InvalidOperationException("本地代理未启用。");
            if (!localSettings.Enabled)
            {
                throw new InvalidOperationException("本地代理未在设置中启用，请先到设置 → AI 模型 → 本地代理开启。");
            }

            List<AiToolDefinition> tools =
            [
                .. toolCatalog.Definitions,
                .. writeToolCatalog.Definitions,
                .. localToolCatalog.GetAllowedTools(localSettings),
            ];
            if (subagentDepth < LocalAgentAiToolCatalog.MaximumSubagentDepth)
            {
                tools.Add(LocalAgentAiToolCatalog.SpawnSubagentTool);
            }

            return tools;
        }

        return mode == AiAssistantMode.Write
            ? [.. toolCatalog.Definitions, .. writeToolCatalog.Definitions]
            : toolCatalog.Definitions;
    }

    public async Task<AiAssistantResult> SendAsync(
        AiAssistantRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserMessage);
        AiProviderSettings settings = getSettings();
        AiProviderPrivacyPolicy.EnsureAllowed(settings);
        Uri endpoint = CreateEndpoint(settings);
        string? apiKey = AiProviderSettings.RequiresApiKey(settings.Provider)
            ? await secretStore.ReadAsync(
                AiProviderSettings.GetCredentialIdentifier(settings.Provider),
                cancellationToken)
            : null;
        if (AiProviderSettings.RequiresApiKey(settings.Provider)
            && string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("请先在设置中保存 API Key。");
        }

        IReadOnlyList<AiToolDefinition> tools = ResolveTools(request.Mode, request.SubagentDepth);
        AiRequestContext context = MinimalAiContextBuilder.Build(
            request.UserMessage,
            tools,
            request.Mode,
            timeProvider.GetLocalNow(),
            request.Conversation);
        var proposals = new List<AiAssistantProposal>();
        string text = settings.Provider switch
        {
            AiProviderKind.OpenAiResponses => await SendResponsesAsync(
                settings, endpoint, apiKey!, context, request, proposals, cancellationToken),
            AiProviderKind.Anthropic => await SendAnthropicAsync(
                settings, endpoint, apiKey!, context, request, proposals, cancellationToken),
            AiProviderKind.OpenAiCompatible or AiProviderKind.Ollama => await SendChatAsync(
                settings, endpoint, apiKey, context, request, proposals, cancellationToken),
            _ => throw new InvalidOperationException("不支持的 AI 提供商。"),
        };
        return new AiAssistantResult(text, proposals);
    }

    public async Task<string> SendAsync(
        string userMessage,
        CancellationToken cancellationToken)
    {
        AiAssistantResult result = await SendAsync(
            new AiAssistantRequest(userMessage, AiAssistantMode.ReadOnly),
            cancellationToken);
        return result.Text;
    }

    private async Task<string> SendChatAsync(
        AiProviderSettings settings,
        Uri endpoint,
        string? apiKey,
        AiRequestContext context,
        AiAssistantRequest request,
        List<AiAssistantProposal> proposals,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray
        {
            CreateMessage("system", context.SystemInstruction),
        };
        AddConversationMessages(messages, context.Conversation);
        messages.Add(CreateMessage("user", context.UserMessage));
        JsonArray tools = CreateChatTools(context.Tools);

        // P72（用户裁决）：不设工具轮次上限，由模型自行收敛；
        // 失控风险由调用方取消（停止按钮 → cancellationToken）兜底。
        for (int round = 0; ; round++)
        {
            var body = new JsonObject
            {
                ["model"] = settings.Model,
                ["messages"] = messages.DeepClone(),
                ["tools"] = tools.DeepClone(),
            };
            if (settings.Provider == AiProviderKind.Ollama)
            {
                body["stream"] = false;
            }

            JsonObject root = await PostToolRoundAsync(
                settings.Provider, endpoint, apiKey, body, messages, round, cancellationToken);
            JsonNode? messageNode = settings.Provider == AiProviderKind.Ollama
                ? root["message"]
                : root["choices"]?[0]?["message"];
            JsonObject message = messageNode?.AsObject()
                ?? throw new InvalidDataException("模型响应缺少 assistant message。");
            ToolCall[] toolCalls = ParseChatToolCalls(message);
            if (toolCalls.Length == 0)
            {
                return message["content"]?.GetValue<string>() ?? string.Empty;
            }

            messages.Add(message.DeepClone());
            foreach (ToolCall toolCall in toolCalls)
            {
                string result = await ExecuteApprovedToolAsync(
                    toolCall, request, proposals, cancellationToken);
                messages.Add(CreateChatToolMessage(settings.Provider, toolCall, result));
            }
        }
    }

    /// <summary>
    /// 工具轮次请求：部分 OpenAI 兼容服务端对回传的 assistant 工具消息
    /// 校验严格（如拒绝 reasoning_content 或空 content），遇到 400 时
    /// 降级为最保守格式（去掉 reasoning_content 与 content）重试一次。
    /// </summary>
    private async Task<JsonObject> PostToolRoundAsync(
        AiProviderKind provider,
        Uri endpoint,
        string? apiKey,
        JsonObject body,
        JsonArray messages,
        int round,
        CancellationToken cancellationToken)
    {
        bool sanitized = false;
        while (true)
        {
            try
            {
                return await PostAsync(provider, endpoint, apiKey, body, cancellationToken);
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode == HttpStatusCode.BadRequest
                    && round > 0
                    && !sanitized)
            {
                sanitized = true;
                SanitizeToolEchoes(messages);
                body["messages"] = messages.DeepClone().AsArray();
            }
        }
    }

    internal static void SanitizeToolEchoes(JsonArray messages)
    {
        foreach (JsonObject message in messages.OfType<JsonObject>()
            .Where(message => message.ContainsKey("tool_calls")))
        {
            message.Remove("reasoning_content");
            message.Remove("content");
        }
    }

    private async Task<string> SendResponsesAsync(
        AiProviderSettings settings,
        Uri endpoint,
        string apiKey,
        AiRequestContext context,
        AiAssistantRequest request,
        List<AiAssistantProposal> proposals,
        CancellationToken cancellationToken)
    {
        JsonArray input = CreateConversationMessages(context.Conversation, context.UserMessage);
        JsonArray tools = CreateOpenAiTools(context.Tools);
        string? previousResponseId = null;

        for (int round = 0; ; round++)
        {
            var body = new JsonObject
            {
                ["model"] = settings.Model,
                ["instructions"] = context.SystemInstruction,
                ["input"] = input.DeepClone(),
                ["tools"] = tools.DeepClone(),
            };
            if (previousResponseId is not null)
            {
                body["previous_response_id"] = previousResponseId;
            }

            JsonObject root = await PostAsync(
                settings.Provider, endpoint, apiKey, body, cancellationToken);
            ToolCall[] toolCalls = ParseResponsesToolCalls(root);
            if (toolCalls.Length == 0)
            {
                return ExtractResponsesText(root);
            }

            previousResponseId = root["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("Responses API 响应缺少 id。");
            input = [];
            foreach (ToolCall toolCall in toolCalls)
            {
                string result = await ExecuteApprovedToolAsync(
                    toolCall, request, proposals, cancellationToken);
                input.Add(new JsonObject
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = toolCall.Id,
                    ["output"] = result,
                });
            }
        }
    }

    private async Task<string> SendAnthropicAsync(
        AiProviderSettings settings,
        Uri endpoint,
        string apiKey,
        AiRequestContext context,
        AiAssistantRequest request,
        List<AiAssistantProposal> proposals,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray();
        AddConversationMessages(messages, context.Conversation);
        messages.Add(CreateMessage("user", context.UserMessage));
        JsonArray tools = CreateAnthropicTools(context.Tools);

        for (int round = 0; ; round++)
        {
            var body = new JsonObject
            {
                ["model"] = settings.Model,
                ["max_tokens"] = 1024,
                ["system"] = context.SystemInstruction,
                ["messages"] = messages.DeepClone(),
                ["tools"] = tools.DeepClone(),
            };
            JsonObject root = await PostAsync(
                settings.Provider, endpoint, apiKey, body, cancellationToken);
            JsonArray content = root["content"]?.AsArray()
                ?? throw new InvalidDataException("Anthropic 响应缺少 content。");
            ToolCall[] toolCalls = ParseAnthropicToolCalls(content);
            if (toolCalls.Length == 0)
            {
                return ExtractAnthropicText(content);
            }

            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = content.DeepClone(),
            });
            var toolResults = new JsonArray();
            foreach (ToolCall toolCall in toolCalls)
            {
                string result = await ExecuteApprovedToolAsync(
                    toolCall, request, proposals, cancellationToken);
                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolCall.Id,
                    ["content"] = result,
                });
            }

            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }
    }

    private async Task<JsonObject> PostAsync(
        AiProviderKind provider,
        Uri endpoint,
        string? apiKey,
        JsonObject body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        ApplyAuthentication(request, provider, apiKey);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessWithApiErrorAsync(response, cancellationToken);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        JsonNode root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("模型返回了空响应。");
        return root.AsObject();
    }

    internal static async Task EnsureSuccessWithApiErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string detail = ExtractApiErrorMessage(body);
        throw new HttpRequestException(
            $"模型接口返回 {(int)response.StatusCode} ({response.StatusCode})：{detail}",
            null,
            response.StatusCode);
    }

    internal static string ExtractApiErrorMessage(string body)
    {
        try
        {
            JsonNode? messageNode = JsonNode.Parse(body)?["error"]?["message"];
            if (messageNode?.GetValue<string>() is { Length: > 0 } message)
            {
                return message;
            }
        }
        catch (JsonException)
        {
            // Fall through to the raw body fallback for non-JSON providers.
        }
        catch (InvalidOperationException)
        {
            // The error node is not a string; fall back to the raw body.
        }

        string trimmed = body.Trim();
        return trimmed.Length == 0
            ? "接口未返回错误详情。"
            : trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    // P71-02：审批等待的默认超时——用户离开时不能让请求无限期挂起，超时默认拒绝。
    private static readonly TimeSpan DefaultToolApprovalTimeout = TimeSpan.FromMinutes(2);

    /// <summary>工具是否命中"总是允许"白名单（P71-02）：命中则不弹审批直接执行。</summary>
    private bool IsToolAlwaysAllowed(string toolName)
    {
        return getLocalAgentSettings?.Invoke()?.AlwaysAllowTools.Contains(toolName) == true;
    }

    /// <summary>用户拒绝时的工具结果（P71-02）：返回 error JSON 让模型自纠，不抛异常。</summary>
    private static string ToolDeniedResult(string toolName)
    {
        return "{\"error\":\"用户已拒绝执行工具 " + toolName + "，请勿重试同一调用\"}";
    }

    /// <summary>
    /// 等待宿主 UI 的审批裁决（P71-02）：未接线回调时安全默认拒绝；超时默认拒绝；
    /// 外层取消（用户停止）原样上抛保持既有取消语义。
    /// </summary>
    private static async Task<AiToolApprovalDecision> RequestToolApprovalAsync(
        AiAssistantRequest request,
        string toolName,
        CancellationToken cancellationToken)
    {
        if (request.ToolApproval is null)
        {
            return AiToolApprovalDecision.Deny;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.ApprovalTimeout ?? DefaultToolApprovalTimeout);
        try
        {
            return await request
                .ToolApproval(new AiToolApprovalRequest(toolName), timeout.Token)
                .WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 超时（或回调自身取消）：默认拒绝。
            return AiToolApprovalDecision.Deny;
        }
    }

    /// <summary>
    /// 非流式路径的工具执行包装（P71-02）：需审批工具先裁决，拒绝返回 error JSON。
    /// 流式路径在 ExecuteStreamingToolAsync 内联同一逻辑（要多发 PendingApproval 事件）。
    /// P71-03：非流式路径没有 onUpdate，子代理事件丢弃、只返回答案文本。
    /// </summary>
    private async Task<string> ExecuteApprovedToolAsync(
        ToolCall toolCall,
        AiAssistantRequest request,
        List<AiAssistantProposal> proposals,
        CancellationToken cancellationToken)
    {
        if (request.Mode == AiAssistantMode.LocalAgent
            && LocalAgentAiToolCatalog.RequiresApproval(toolCall.Name)
            && !IsToolAlwaysAllowed(toolCall.Name)
            && await RequestToolApprovalAsync(request, toolCall.Name, cancellationToken)
                == AiToolApprovalDecision.Deny)
        {
            return ToolDeniedResult(toolCall.Name);
        }

        return await ExecuteToolAsync(
            toolCall, request, proposals, onUpdate: null, cancellationToken);
    }

    /// <summary>
    /// P71-03：执行 spawn_subagent——同 provider/model 开子会话（SubagentDepth+1、
    /// 透传审批回调），子会话的流式事件经包装委托转发到宿主（onUpdate 为 null 的
    /// 非流式路径丢弃事件只返回答案）。返回子代理最终答复文本作为工具结果。
    /// </summary>
    private async Task<string> ExecuteSubagentAsync(
        ToolCall toolCall,
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task>? onUpdate,
        CancellationToken cancellationToken)
    {
        string description = GetOptionalString(toolCall.Arguments, "description") ?? "子代理";
        string task = GetRequiredString(toolCall.Arguments, "task");
        if (request.SubagentDepth >= LocalAgentAiToolCatalog.MaximumSubagentDepth)
        {
            // 深度守卫：目录层已达上限不再注入 spawn，这里兜底模型幻觉调用。
            return "{\"error\":\"子代理嵌套已达上限 2 层，不可再委派\"}";
        }

        Guid subagentId = Guid.NewGuid();
        Task Emit(AiAssistantUpdate update, CancellationToken token) =>
            onUpdate?.Invoke(new AiSubagentUpdate(subagentId, description, update), token)
                ?? Task.CompletedTask;
        await Emit(AiSubagentStarted.Instance, cancellationToken);
        var answer = new StringBuilder();
        try
        {
            var subRequest = new AiAssistantRequest(task, request.Mode, toolApproval: request.ToolApproval)
            {
                SubagentDepth = request.SubagentDepth + 1,
                ApprovalTimeout = request.ApprovalTimeout,
            };
            await StreamAsync(
                subRequest,
                (update, token) =>
                {
                    if (update is AiTextDelta { Text.Length: > 0 } delta)
                    {
                        answer.Append(delta.Text);
                    }

                    return Emit(update, token);
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 取消：不发终态事件，卡片由宿主流结束兜底收束（与工具行取消语义一致）。
            throw;
        }
        catch (Exception exception)
        {
            await Emit(new AiSubagentCompleted(false, exception.Message), cancellationToken);
            throw;
        }

        await Emit(new AiSubagentCompleted(true, null), cancellationToken);
        return answer.Length == 0 ? "（子代理未返回内容）" : answer.ToString();
    }

    private async Task<string> ExecuteToolAsync(
        ToolCall toolCall,
        AiAssistantRequest request,
        List<AiAssistantProposal> proposals,
        Func<AiAssistantUpdate, CancellationToken, Task>? onUpdate,
        CancellationToken cancellationToken)
    {
        // P71-03：子代理委派最先分流——子会话事件经 onUpdate 包装转发，不发工具行
        // 事件（UI 中子代理是折叠卡而非工具行）；委派本身不写入，不走审批。
        if (toolCall.Name == LocalAgentAiToolCatalog.SpawnSubagentToolName)
        {
            return await ExecuteSubagentAsync(toolCall, request, onUpdate, cancellationToken);
        }

        // P71-01：本地工具分流到 LocalAgentToolExecutor（静态执行器）。getLocalAgentSettings
        // 为 null 说明宿主没有接线，模型不可能拿到这些工具名，此时显式报错而非静默。
        if (LocalAgentAiToolCatalog.IsLocalTool(toolCall.Name))
        {
            if (getLocalAgentSettings is null)
            {
                throw new InvalidOperationException("本地代理未启用。");
            }

            AiToolExecutionResult localResult = await LocalAgentToolExecutor.ExecuteAsync(
                toolCall.Name,
                toolCall.Arguments,
                cancellationToken);
            return localResult.Json;
        }

        if (!toolCall.Name.StartsWith("propose_", StringComparison.Ordinal))
        {
            AiToolExecutionResult result = await toolExecutor.ExecuteAsync(
                toolCall.Name,
                toolCall.Arguments,
                cancellationToken);
            return result.Json;
        }

        if (request.Mode is not (AiAssistantMode.Write or AiAssistantMode.LocalAgent))
        {
            throw new InvalidOperationException("只读模式不允许写入提案工具。");
        }

        proposals.Add(CreateProposal(toolCall));
        return "{\"status\":\"pending_user_confirmation\"}";
    }

    private static AiAssistantProposal CreateProposal(ToolCall toolCall)
    {
        JsonElement arguments = toolCall.Arguments;
        return toolCall.Name switch
        {
            "propose_timed_event" => new AiCreationAssistantProposal(
                AiCreationDraft.TimedEvent(
                    NormalizeEventTitle(GetRequiredString(arguments, "title"), arguments),
                    GetZonedDateTimeOffset(arguments, "startAt", GetRequiredString(arguments, "timeZoneId")),
                    GetZonedDateTimeOffset(arguments, "endAt", GetRequiredString(arguments, "timeZoneId")),
                    GetRequiredString(arguments, "timeZoneId"),
                    GetOptionalString(arguments, "location"),
                    GetOptionalString(arguments, "meetingNumber"))),
            "propose_all_day_event" => new AiCreationAssistantProposal(
                AiCreationDraft.AllDayEvent(
                    GetRequiredString(arguments, "title"),
                    GetDateOnly(arguments, "startDate"),
                    GetDateOnly(arguments, "endDateExclusive"))),
            "propose_todo" => new AiCreationAssistantProposal(
                AiCreationDraft.Todo(
                    GetRequiredString(arguments, "title"),
                    GetOptionalDateTimeOffset(arguments, "dueAt"))),
            "propose_record" => new AiCreationAssistantProposal(
                AiCreationDraft.Record(
                    GetRequiredString(arguments, "title"),
                    GetEnum<WorkRecordType>(arguments, "recordType"),
                    GetRequiredString(arguments, "content"))),
            "propose_todo_status" => new AiChangeAssistantProposal(
                new AiChangeSetDraft(
                    "更新待办状态",
                    [new AiTodoStatusChange(
                        GetGuid(arguments, "todoId"),
                        GetEnum<TodoStatus>(arguments, "status"))])),
            "propose_delete" => new AiChangeAssistantProposal(
                new AiChangeSetDraft(
                    "移至回收站",
                    [new AiRecycleChange(
                        GetEnum<AiEntityKind>(arguments, "entityKind"),
                        GetGuid(arguments, "entityId"))])),
            _ => throw new InvalidOperationException($"Tool '{toolCall.Name}' is not allowed."),
        };
    }

    private static string GetRequiredString(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"提案参数缺少 {propertyName}。");
        }

        return value.GetString()!;
    }

    private static string? GetOptionalString(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"提案参数 {propertyName} 必须是字符串或 null。");
        }

        string? text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string NormalizeEventTitle(string title, JsonElement arguments)
    {
        string normalized = title.Trim();
        string? meetingNumber = GetOptionalString(arguments, "meetingNumber");
        int separator = normalized.IndexOf("http", StringComparison.OrdinalIgnoreCase);
        if (separator > 0)
        {
            normalized = normalized[..separator];
        }

        int meetingMarker = normalized.IndexOf("#腾讯会议", StringComparison.Ordinal);
        if (meetingMarker > 0)
        {
            normalized = normalized[..meetingMarker];
        }

        if (!string.IsNullOrWhiteSpace(meetingNumber))
        {
            int numberIndex = normalized.IndexOf(meetingNumber, StringComparison.Ordinal);
            if (numberIndex > 0)
            {
                normalized = normalized[..numberIndex];
            }

            int tencentMarker = normalized.IndexOf("腾讯会议", StringComparison.Ordinal);
            if (tencentMarker > 0)
            {
                normalized = normalized[..tencentMarker];
            }
        }

        return normalized.Trim().TrimEnd('|', '｜', '-', '—', ':', '：', '，', ',');
    }

    private static DateTimeOffset GetDateTimeOffset(JsonElement arguments, string propertyName)
    {
        return DateTimeOffset.Parse(
            GetRequiredString(arguments, propertyName),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    /// <summary>
    /// 解析日程时间并按声明的时区锚定：模型常把用户本地墙钟时间误标成 Z 后缀（零偏移），
    /// 此时按 timeZoneId 的墙钟时间重新锚定，避免创建的日程整体偏移时区差。
    /// </summary>
    private static DateTimeOffset GetZonedDateTimeOffset(
        JsonElement arguments,
        string propertyName,
        string timeZoneId)
    {
        DateTimeOffset parsed = GetDateTimeOffset(arguments, propertyName);
        if (parsed.Offset != TimeSpan.Zero)
        {
            return parsed;
        }

        try
        {
            TimeSpan zoneOffset = TimeZoneInfo
                .FindSystemTimeZoneById(timeZoneId)
                .GetUtcOffset(parsed);
            if (zoneOffset != TimeSpan.Zero)
            {
                return new DateTimeOffset(
                    DateTime.SpecifyKind(parsed.DateTime, DateTimeKind.Unspecified),
                    zoneOffset);
            }
        }
        catch (TimeZoneNotFoundException)
        {
            // 模型给出的时区 ID 无法识别时保留原解析结果。
        }
        catch (InvalidTimeZoneException)
        {
        }

        return parsed;
    }

    private static DateTimeOffset? GetOptionalDateTimeOffset(
        JsonElement arguments,
        string propertyName)
    {
        return !arguments.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind == JsonValueKind.Null
            ? null
            : DateTimeOffset.Parse(
                value.GetString()!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
    }

    private static DateOnly GetDateOnly(JsonElement arguments, string propertyName)
    {
        return DateOnly.ParseExact(
            GetRequiredString(arguments, propertyName),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);
    }

    private static Guid GetGuid(JsonElement arguments, string propertyName)
    {
        return Guid.Parse(GetRequiredString(arguments, propertyName));
    }

    private static TEnum GetEnum<TEnum>(JsonElement arguments, string propertyName)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse(GetRequiredString(arguments, propertyName), out TEnum value))
        {
            throw new InvalidDataException($"提案参数 {propertyName} 无效。");
        }

        return value;
    }

    private static void ApplyAuthentication(
        HttpRequestMessage request,
        AiProviderKind provider,
        string? apiKey)
    {
        if (provider is AiProviderKind.OpenAiCompatible or AiProviderKind.OpenAiResponses)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        else if (provider == AiProviderKind.Anthropic)
        {
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
    }

    private static ToolCall[] ParseChatToolCalls(JsonObject message)
    {
        if (message["tool_calls"] is not JsonArray calls)
        {
            return [];
        }

        var result = new List<ToolCall>();
        foreach (JsonNode? callNode in calls)
        {
            JsonObject call = callNode?.AsObject()
                ?? throw new InvalidDataException("模型工具调用格式无效。");
            JsonObject function = call["function"]?.AsObject()
                ?? throw new InvalidDataException("模型工具调用缺少 function。");
            result.Add(CreateToolCall(
                call["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                function["name"],
                function["arguments"]));
        }

        return result.ToArray();
    }

    private static ToolCall[] ParseResponsesToolCalls(JsonObject root)
    {
        if (root["output"] is not JsonArray output)
        {
            throw new InvalidDataException("Responses API 响应缺少 output。");
        }

        return [.. output
            .OfType<JsonObject>()
            .Where(item => item["type"]?.GetValue<string>() == "function_call")
            .Select(item => CreateToolCall(
                item["call_id"]?.GetValue<string>()
                    ?? throw new InvalidDataException("Responses 工具调用缺少 call_id。"),
                item["name"],
                item["arguments"]))];
    }

    private static ToolCall[] ParseAnthropicToolCalls(JsonArray content)
    {
        return [.. content
            .OfType<JsonObject>()
            .Where(item => item["type"]?.GetValue<string>() == "tool_use")
            .Select(item => CreateToolCall(
                item["id"]?.GetValue<string>()
                    ?? throw new InvalidDataException("Anthropic 工具调用缺少 id。"),
                item["name"],
                item["input"]))];
    }

    private static ToolCall CreateToolCall(string id, JsonNode? nameNode, JsonNode? argumentsNode)
    {
        string name = nameNode?.GetValue<string>()
            ?? throw new InvalidDataException("模型工具调用缺少名称。");
        string argumentJson = argumentsNode switch
        {
            JsonValue value when value.TryGetValue(out string? text) => text ?? "{}",
            null => "{}",
            _ => argumentsNode.ToJsonString(),
        };
        using JsonDocument arguments = JsonDocument.Parse(argumentJson);
        return new ToolCall(id, name, arguments.RootElement.Clone());
    }

    private static string ExtractResponsesText(JsonObject root)
    {
        if (root["output"] is not JsonArray output)
        {
            throw new InvalidDataException("Responses API 响应缺少 output。");
        }

        return string.Concat(output
            .OfType<JsonObject>()
            .Where(item => item["type"]?.GetValue<string>() == "message")
            .SelectMany(item => item["content"]?.AsArray().OfType<JsonObject>() ?? [])
            .Where(item => item["type"]?.GetValue<string>() == "output_text")
            .Select(item => item["text"]?.GetValue<string>() ?? string.Empty));
    }

    private static string ExtractAnthropicText(JsonArray content)
    {
        return string.Concat(content
            .OfType<JsonObject>()
            .Where(item => item["type"]?.GetValue<string>() == "text")
            .Select(item => item["text"]?.GetValue<string>() ?? string.Empty));
    }

    private static JsonArray CreateChatTools(IReadOnlyList<AiToolDefinition> definitions)
    {
        var tools = new JsonArray();
        foreach (AiToolDefinition definition in definitions)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = definition.Name,
                    ["description"] = definition.Description,
                    ["parameters"] = JsonNode.Parse(definition.ParametersJsonSchema),
                },
            });
        }

        return tools;
    }

    private static JsonArray CreateOpenAiTools(IReadOnlyList<AiToolDefinition> definitions)
    {
        var tools = new JsonArray();
        foreach (AiToolDefinition definition in definitions)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["name"] = definition.Name,
                ["description"] = definition.Description,
                ["parameters"] = JsonNode.Parse(definition.ParametersJsonSchema),
            });
        }

        return tools;
    }

    private static JsonArray CreateAnthropicTools(IReadOnlyList<AiToolDefinition> definitions)
    {
        var tools = new JsonArray();
        foreach (AiToolDefinition definition in definitions)
        {
            tools.Add(new JsonObject
            {
                ["name"] = definition.Name,
                ["description"] = definition.Description,
                ["input_schema"] = JsonNode.Parse(definition.ParametersJsonSchema),
            });
        }

        return tools;
    }

    private static void AddConversationMessages(
        JsonArray messages,
        IReadOnlyList<AiAssistantTurn> conversation)
    {
        foreach (AiAssistantTurn turn in conversation)
        {
            if (!string.IsNullOrWhiteSpace(turn.Text))
            {
                messages.Add(CreateMessage(turn.IsUser ? "user" : "assistant", turn.Text));
            }
        }
    }

    private static JsonArray CreateConversationMessages(
        IReadOnlyList<AiAssistantTurn> conversation,
        string currentMessage)
    {
        var messages = new JsonArray();
        AddConversationMessages(messages, conversation);
        messages.Add(CreateMessage("user", currentMessage));
        return messages;
    }

    private static JsonObject CreateMessage(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    private static JsonObject CreateChatToolMessage(
        AiProviderKind provider,
        ToolCall toolCall,
        string result)
    {
        var message = CreateMessage("tool", result);
        if (provider == AiProviderKind.OpenAiCompatible)
        {
            message["tool_call_id"] = toolCall.Id;
        }
        else
        {
            message["tool_name"] = toolCall.Name;
        }

        return message;
    }

    private static Uri CreateEndpoint(AiProviderSettings settings)
    {
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out Uri? endpoint))
        {
            throw new InvalidOperationException("模型端点地址无效。");
        }

        string relativePath = settings.Provider switch
        {
            AiProviderKind.OpenAiCompatible => "chat/completions",
            AiProviderKind.OpenAiResponses => "responses",
            AiProviderKind.Anthropic => "messages",
            AiProviderKind.Ollama => "api/chat",
            _ => throw new ArgumentOutOfRangeException(nameof(settings)),
        };
        return new Uri(new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/"), relativePath);
    }

    private sealed record ToolCall(string Id, string Name, JsonElement Arguments);
}
