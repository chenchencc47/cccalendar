using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Infrastructure.AI;

public sealed partial class ProviderAiAssistantClient
{
    public async Task StreamAsync(
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onUpdate);
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
        // P74-02：流开始前先发一次上下文构成估算，宿主渲染 token 分段条。
        await onUpdate(
            AiTokenEstimator.EstimateComposition(context),
            cancellationToken);
        switch (settings.Provider)
        {
            case AiProviderKind.OpenAiResponses:
                await StreamResponsesAsync(
                    settings, endpoint, apiKey!, context, request, onUpdate, cancellationToken);
                break;
            case AiProviderKind.Anthropic:
                await StreamAnthropicAsync(
                    settings, endpoint, apiKey!, context, request, onUpdate, cancellationToken);
                break;
            case AiProviderKind.OpenAiCompatible:
                await StreamOpenAiChatAsync(
                    settings, endpoint, apiKey, context, request, onUpdate, cancellationToken);
                break;
            case AiProviderKind.Ollama:
                await StreamOllamaAsync(
                    settings, endpoint, context, request, onUpdate, cancellationToken);
                break;
            default:
                throw new InvalidOperationException("不支持的 AI 提供商。");
        }
    }

    private async Task StreamOpenAiChatAsync(
        AiProviderSettings settings,
        Uri endpoint,
        string? apiKey,
        AiRequestContext context,
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
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
                ["stream"] = true,
                // 要求服务端在流末尾回 usage chunk；个别 OpenAI 兼容网关不认识该字段
                // 可能 400（已知风险），缺失 usage 时静默不发事件兜底（P70-03）。
                ["stream_options"] = new JsonObject { ["include_usage"] = true },
            };
            StreamedChatResponse streamed = await PostStreamToolRoundAsync(
                settings.Provider,
                endpoint,
                apiKey,
                body,
                messages,
                round,
                (reader, token) => ReadOpenAiChatStreamAsync(reader, onUpdate, token),
                cancellationToken);
            if (streamed.ToolCalls.Count == 0)
            {
                return;
            }

            messages.Add(CreateAssistantToolMessage(streamed.ToolCalls, streamed.ReasoningContent));
            foreach (ToolCall toolCall in streamed.ToolCalls)
            {
                string result = await ExecuteStreamingToolAsync(
                    toolCall, request, onUpdate, cancellationToken);
                messages.Add(CreateChatToolMessage(settings.Provider, toolCall, result));
            }
        }
    }

    /// <summary>
    /// 流式工具轮次请求：回传 assistant 工具消息遇 400 时降级为
    /// 最保守格式（去掉 reasoning_content 与 content）重试一次，
    /// 兼容对回传校验严格的 OpenAI 兼容服务端。
    /// </summary>
    private async Task<StreamedChatResponse> PostStreamToolRoundAsync(
        AiProviderKind provider,
        Uri endpoint,
        string? apiKey,
        JsonObject body,
        JsonArray messages,
        int round,
        Func<StreamReader, CancellationToken, Task<StreamedChatResponse>> readResponse,
        CancellationToken cancellationToken)
    {
        bool sanitized = false;
        while (true)
        {
            try
            {
                return await PostStreamAsync(
                    provider, endpoint, apiKey, body, readResponse, cancellationToken);
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

    private static async Task<StreamedChatResponse> ReadOpenAiChatStreamAsync(
        StreamReader reader,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var calls = new Dictionary<int, StreamingToolCall>();
        var reasoning = new StringBuilder();
        await ReadSseAsync(
            reader,
            async (_, data, token) =>
            {
                if (data == "[DONE]")
                {
                    return;
                }

                JsonObject root = JsonNode.Parse(data)?.AsObject()
                    ?? throw new InvalidDataException("OpenAI 流返回了空事件。");
                // usage chunk 的 choices 为空数组，必须先于 delta 判空处理；
                // 网关忽略 include_usage 时不回 usage，静默不发（P70-03）。
                if (TryCreateOpenAiChatUsage(root) is { } usage)
                {
                    await onUpdate(usage, token);
                }

                // usage chunk 的 choices 是空数组，JsonArray 越界索引会抛异常，须判空。
                JsonObject? delta = root["choices"] is JsonArray { Count: > 0 } choices
                    ? choices[0]?["delta"]?.AsObject()
                    : null;
                if (delta is null)
                {
                    return;
                }

                string? text = delta["content"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    await onUpdate(new AiTextDelta(text), token);
                }

                string? reasoningDelta = delta["reasoning_content"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(reasoningDelta))
                {
                    reasoning.Append(reasoningDelta);
                    await onUpdate(new AiReasoningDelta(reasoningDelta), token);
                }

                if (delta["tool_calls"] is not JsonArray toolCalls)
                {
                    return;
                }

                foreach (JsonObject callNode in toolCalls.OfType<JsonObject>())
                {
                    int index = callNode["index"]?.GetValue<int>() ?? calls.Count;
                    if (!calls.TryGetValue(index, out StreamingToolCall? call))
                    {
                        call = new StreamingToolCall();
                        calls[index] = call;
                    }

                    call.Id ??= callNode["id"]?.GetValue<string>();
                    JsonObject? function = callNode["function"]?.AsObject();
                    call.Name ??= function?["name"]?.GetValue<string>();
                    call.AppendArguments(function?["arguments"]?.GetValue<string>());
                }
            },
            cancellationToken);
        return new StreamedChatResponse(
            ToToolCalls(calls.OrderBy(item => item.Key).Select(item => item.Value)),
            reasoning.Length == 0 ? null : reasoning.ToString());
    }

    private async Task StreamOllamaAsync(
        AiProviderSettings settings,
        Uri endpoint,
        AiRequestContext context,
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray
        {
            CreateMessage("system", context.SystemInstruction),
        };
        AddConversationMessages(messages, context.Conversation);
        messages.Add(CreateMessage("user", context.UserMessage));
        JsonArray tools = CreateChatTools(context.Tools);
        for (int round = 0; ; round++)
        {
            var body = new JsonObject
            {
                ["model"] = settings.Model,
                ["messages"] = messages.DeepClone(),
                ["tools"] = tools.DeepClone(),
                ["stream"] = true,
            };
            StreamedChatResponse streamed = await PostStreamAsync(
                settings.Provider,
                endpoint,
                null,
                body,
                (reader, token) => ReadOllamaStreamAsync(reader, onUpdate, token),
                cancellationToken);
            if (streamed.ToolCalls.Count == 0)
            {
                return;
            }

            messages.Add(CreateAssistantToolMessage(streamed.ToolCalls));
            foreach (ToolCall toolCall in streamed.ToolCalls)
            {
                string result = await ExecuteStreamingToolAsync(
                    toolCall, request, onUpdate, cancellationToken);
                messages.Add(CreateChatToolMessage(settings.Provider, toolCall, result));
            }
        }
    }

    private static async Task<StreamedChatResponse> ReadOllamaStreamAsync(
        StreamReader reader,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var calls = new List<ToolCall>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonObject root = JsonNode.Parse(line)?.AsObject()
                ?? throw new InvalidDataException("Ollama 流返回了空事件。");
            // prompt_eval_count/eval_count 只出现在最终 chunk，先于 message 判空，
            // 避免无 message 的收尾 chunk 丢用量；中间 chunk 无计数则静默（P70-03）。
            if (TryCreateOllamaUsage(root) is { } usage)
            {
                await onUpdate(usage, cancellationToken);
            }

            JsonObject? message = root["message"]?.AsObject();
            if (message is null)
            {
                continue;
            }

            string? text = message["content"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(text))
            {
                await onUpdate(new AiTextDelta(text), cancellationToken);
            }

            string? reasoning = message["thinking"]?.GetValue<string>()
                ?? message["reasoning_content"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(reasoning))
            {
                await onUpdate(new AiReasoningDelta(reasoning), cancellationToken);
            }

            calls.AddRange(ParseChatToolCalls(message));
        }

        return new StreamedChatResponse(calls);
    }

    private async Task StreamResponsesAsync(
        AiProviderSettings settings,
        Uri endpoint,
        string apiKey,
        AiRequestContext context,
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
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
                ["stream"] = true,
            };
            if (previousResponseId is not null)
            {
                body["previous_response_id"] = previousResponseId;
            }

            StreamedResponsesResponse streamed = await PostStreamAsync(
                settings.Provider,
                endpoint,
                apiKey,
                body,
                (reader, token) => ReadResponsesStreamAsync(reader, onUpdate, token),
                cancellationToken);
            if (streamed.ToolCalls.Count == 0)
            {
                return;
            }

            previousResponseId = streamed.ResponseId
                ?? throw new InvalidDataException("Responses 流缺少 response id。");
            input = [];
            foreach (ToolCall toolCall in streamed.ToolCalls)
            {
                string result = await ExecuteStreamingToolAsync(
                    toolCall, request, onUpdate, cancellationToken);
                input.Add(new JsonObject
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = toolCall.Id,
                    ["output"] = result,
                });
            }
        }
    }

    private static async Task<StreamedResponsesResponse> ReadResponsesStreamAsync(
        StreamReader reader,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var calls = new Dictionary<string, StreamingToolCall>(StringComparer.Ordinal);
        string? responseId = null;
        await ReadSseAsync(
            reader,
            async (eventName, data, token) =>
            {
                JsonObject root = JsonNode.Parse(data)?.AsObject()
                    ?? throw new InvalidDataException("Responses 流返回了空事件。");
                string type = eventName ?? root["type"]?.GetValue<string>() ?? string.Empty;
                if (type == "response.output_text.delta")
                {
                    string? text = root["delta"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(text))
                    {
                        await onUpdate(new AiTextDelta(text), token);
                    }

                    return;
                }

                if (type == "response.output_item.added"
                    && root["item"] is JsonObject item
                    && item["type"]?.GetValue<string>() == "function_call")
                {
                    string key = item["id"]?.GetValue<string>()
                        ?? root["output_index"]?.ToJsonString()
                        ?? Guid.NewGuid().ToString("N");
                    var call = new StreamingToolCall
                    {
                        Id = item["call_id"]?.GetValue<string>(),
                        Name = item["name"]?.GetValue<string>(),
                    };
                    call.AppendArguments(item["arguments"]?.GetValue<string>());
                    calls[key] = call;
                    return;
                }

                if (type == "response.function_call_arguments.delta")
                {
                    string key = root["item_id"]?.GetValue<string>()
                        ?? root["output_index"]?.ToJsonString()
                        ?? throw new InvalidDataException("Responses 工具参数增量缺少项目标识。");
                    if (!calls.TryGetValue(key, out StreamingToolCall? call))
                    {
                        call = new StreamingToolCall();
                        calls[key] = call;
                    }

                    call.AppendArguments(root["delta"]?.GetValue<string>());
                    return;
                }

                if (type == "response.completed" && root["response"] is JsonObject response)
                {
                    responseId = response["id"]?.GetValue<string>();
                    AddCompletedResponseCalls(response, calls);
                    if (TryCreateResponsesUsage(response) is { } usage)
                    {
                        await onUpdate(usage, token);
                    }
                }
            },
            cancellationToken);
        return new StreamedResponsesResponse(responseId, ToToolCalls(calls.Values));
    }

    private async Task StreamAnthropicAsync(
        AiProviderSettings settings,
        Uri endpoint,
        string apiKey,
        AiRequestContext context,
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
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
                ["stream"] = true,
            };
            StreamedAnthropicResponse streamed = await PostStreamAsync(
                settings.Provider,
                endpoint,
                apiKey,
                body,
                (reader, token) => ReadAnthropicStreamAsync(reader, onUpdate, token),
                cancellationToken);
            if (streamed.ToolCalls.Count == 0)
            {
                return;
            }

            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = CreateAnthropicToolUseContent(streamed.ToolCalls),
            });
            var toolResults = new JsonArray();
            foreach (ToolCall toolCall in streamed.ToolCalls)
            {
                string result = await ExecuteStreamingToolAsync(
                    toolCall, request, onUpdate, cancellationToken);
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

    private static async Task<StreamedAnthropicResponse> ReadAnthropicStreamAsync(
        StreamReader reader,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var calls = new Dictionary<int, StreamingToolCall>();
        // Anthropic 把 usage 拆两段：message_start 带 input/cache 计数，message_delta
        // 才有最终 output_tokens，暂存后合并成一次完整事件发射（P70-03）。
        long? inputTokens = null;
        long? cachedTokens = null;
        long? cacheWriteTokens = null;
        string? model = null;
        await ReadSseAsync(
            reader,
            async (_, data, token) =>
            {
                JsonObject root = JsonNode.Parse(data)?.AsObject()
                    ?? throw new InvalidDataException("Anthropic 流返回了空事件。");
                string type = root["type"]?.GetValue<string>() ?? string.Empty;
                int index = root["index"]?.GetValue<int>() ?? 0;
                if (type == "message_start" && root["message"] is JsonObject startMessage)
                {
                    if (startMessage["usage"] is JsonObject startUsage)
                    {
                        inputTokens = GetTokenCount(startUsage, "input_tokens");
                        cachedTokens = GetTokenCount(startUsage, "cache_read_input_tokens");
                        cacheWriteTokens = GetTokenCount(startUsage, "cache_creation_input_tokens");
                    }

                    model ??= startMessage["model"]?.GetValue<string>();
                    return;
                }

                if (type == "message_delta" && root["usage"] is JsonObject deltaUsage)
                {
                    // 两处都无 usage 视为提供商未上报，静默不发。
                    if (inputTokens is not null || GetTokenCount(deltaUsage, "output_tokens") is not null)
                    {
                        await onUpdate(new AiUsageUpdate(
                            inputTokens ?? 0,
                            GetTokenCount(deltaUsage, "output_tokens") ?? 0,
                            cachedTokens,
                            cacheWriteTokens,
                            Model: model), token);
                    }

                    return;
                }

                if (type == "content_block_start"
                    && root["content_block"] is JsonObject block
                    && block["type"]?.GetValue<string>() == "tool_use")
                {
                    var call = new StreamingToolCall
                    {
                        Id = block["id"]?.GetValue<string>(),
                        Name = block["name"]?.GetValue<string>(),
                    };
                    if (block["input"] is JsonObject input && input.Count > 0)
                    {
                        call.AppendArguments(input.ToJsonString());
                    }

                    calls[index] = call;
                    return;
                }

                if (type != "content_block_delta" || root["delta"] is not JsonObject delta)
                {
                    return;
                }

                string deltaType = delta["type"]?.GetValue<string>() ?? string.Empty;
                if (deltaType is "thinking_delta" or "reasoning_content_delta")
                {
                    string? reasoning = delta["thinking"]?.GetValue<string>()
                        ?? delta["text"]?.GetValue<string>()
                        ?? delta["reasoning_content"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(reasoning))
                    {
                        await onUpdate(new AiReasoningDelta(reasoning), token);
                    }
                }
                else if (deltaType == "text_delta")
                {
                    string? text = delta["text"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(text))
                    {
                        await onUpdate(new AiTextDelta(text), token);
                    }
                }
                else if (deltaType == "input_json_delta")
                {
                    if (!calls.TryGetValue(index, out StreamingToolCall? call))
                    {
                        call = new StreamingToolCall();
                        calls[index] = call;
                    }

                    call.AppendArguments(delta["partial_json"]?.GetValue<string>());
                }
            },
            cancellationToken);
        return new StreamedAnthropicResponse(ToToolCalls(calls.OrderBy(item => item.Key).Select(item => item.Value)));
    }

    private async Task<string> ExecuteStreamingToolAsync(
        ToolCall toolCall,
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        // P71-03：spawn_subagent 最先分流——委派不发工具行事件（子代理在 UI 是
        // 折叠卡，生命周期由 AiSubagentUpdate 包装事件驱动），也不审批（委派本身
        // 不写入；子代理子会话内的写工具各自走审批）。
        if (toolCall.Name == LocalAgentAiToolCatalog.SpawnSubagentToolName)
        {
            var spawnProposals = new List<AiAssistantProposal>();
            return await ExecuteToolAsync(
                toolCall, request, spawnProposals, onUpdate, cancellationToken);
        }

        // P70-05：工具轮次此前静默执行，UI 只看到最终提案卡；执行前后各发一次
        // 可见性事件，让消息流中出现 ToolRow。摘要的生成与截断规则集中在下面两个 helper。
        string argsSummary = SummarizeToolArguments(toolCall.Arguments);
        // P71-02：写入类本地工具执行前先弹审批——先发 PendingApproval 事件（UI 落
        // 审批行），再等宿主回调裁决；拒绝返回 error JSON 让模型自纠，不算异常。
        if (request.Mode == AiAssistantMode.LocalAgent
            && LocalAgentAiToolCatalog.RequiresApproval(toolCall.Name)
            && !IsToolAlwaysAllowed(toolCall.Name))
        {
            await onUpdate(
                new AiToolCallUpdate(toolCall.Name, argsSummary, AiToolCallState.PendingApproval),
                cancellationToken);
            if (await RequestToolApprovalAsync(request, toolCall.Name, cancellationToken)
                == AiToolApprovalDecision.Deny)
            {
                await onUpdate(
                    new AiToolCallUpdate(toolCall.Name, argsSummary, AiToolCallState.Error, "用户已拒绝"),
                    cancellationToken);
                return ToolDeniedResult(toolCall.Name);
            }
        }

        await onUpdate(
            new AiToolCallUpdate(toolCall.Name, argsSummary, AiToolCallState.Running),
            cancellationToken);
        try
        {
            var proposals = new List<AiAssistantProposal>();
            string result = await ExecuteToolAsync(
                toolCall, request, proposals, onUpdate, cancellationToken);
            foreach (AiAssistantProposal proposal in proposals)
            {
                await onUpdate(new AiProposalUpdate(proposal), cancellationToken);
            }

            await onUpdate(
                new AiToolCallUpdate(
                    toolCall.Name,
                    argsSummary,
                    AiToolCallState.Ok,
                    SummarizeToolResult(result)),
                cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            // 取消不是工具失败：不发 Error 事件（Running 行由 VM 的流结束兜底收束），
            // 原样上抛保持既有取消语义。
            throw;
        }
        catch (Exception exception)
        {
            // 失败也要让用户看到（工具行转红），发完事件后原样上抛，
            // 异常处理流与改动前完全一致。
            await onUpdate(
                new AiToolCallUpdate(
                    toolCall.Name,
                    argsSummary,
                    AiToolCallState.Error,
                    exception.Message),
                cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 工具参数的一行摘要（P70-05）：取前两个键值对用「k: v · k: v」拼接，
    /// 超过 40 字符截断加省略号——工具行空间小，只要能认出模型想干什么即可。
    /// </summary>
    private static string SummarizeToolArguments(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (JsonProperty property in arguments.EnumerateObject().Take(2))
        {
            string value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
            parts.Add($"{property.Name}: {value}");
        }

        string summary = string.Join(" · ", parts);
        return summary.Length <= 40 ? summary : summary[..40] + "…";
    }

    /// <summary>
    /// 工具结果的一行摘要（P70-05）：压平换行取前 60 字符——查询类工具回大 JSON，
    /// 展开区只看形态不看全量。
    /// </summary>
    private static string SummarizeToolResult(string result)
    {
        string oneLine = result.Replace("\r", " ").Replace("\n", " ").Trim();
        return oneLine.Length <= 60 ? oneLine : oneLine[..60] + "…";
    }

    private async Task<T> PostStreamAsync<T>(
        AiProviderKind provider,
        Uri endpoint,
        string? apiKey,
        JsonObject body,
        Func<StreamReader, CancellationToken, Task<T>> readResponse,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        ApplyAuthentication(request, provider, apiKey);
        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessWithApiErrorAsync(response, cancellationToken);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await readResponse(reader, cancellationToken);
    }

    private static async Task ReadSseAsync(
        StreamReader reader,
        Func<string?, string, CancellationToken, Task> handleEvent,
        CancellationToken cancellationToken)
    {
        string? eventName = null;
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    await handleEvent(eventName, data.ToString(), cancellationToken);
                    eventName = null;
                    data.Clear();
                }

                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[6..].TrimStart();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0)
                {
                    data.Append('\n');
                }

                data.Append(line[5..].TrimStart());
            }
        }

        if (data.Length > 0)
        {
            await handleEvent(eventName, data.ToString(), cancellationToken);
        }
    }

    private static IReadOnlyList<ToolCall> ToToolCalls(IEnumerable<StreamingToolCall> calls) =>
        [.. calls.Select(call => call.Build())];

    /// <summary>读取 token 计数；节点缺失或非整数时返回 null（各网关回传字段不一，缺省不视为 0）。</summary>
    private static long? GetTokenCount(JsonObject? container, string property)
    {
        return container?[property] is JsonValue value && value.TryGetValue(out long count)
            ? count
            : null;
    }

    private static long? GetNestedTokenCount(
        JsonObject? container,
        string parentProperty,
        string property) =>
        container?[parentProperty] is JsonObject parent ? GetTokenCount(parent, property) : null;

    /// <summary>
    /// 解析 OpenAI 兼容流的 usage chunk：OpenAI 风格嵌套于 prompt/completion_tokens_details，
    /// DeepSeek 风格平铺 prompt_cache_hit_tokens，两者取有值者；无 usage 返回 null（静默）。
    /// </summary>
    private static AiUsageUpdate? TryCreateOpenAiChatUsage(JsonObject root)
    {
        if (root["usage"] is not JsonObject usage)
        {
            return null;
        }

        return new AiUsageUpdate(
            GetTokenCount(usage, "prompt_tokens") ?? 0,
            GetTokenCount(usage, "completion_tokens") ?? 0,
            GetNestedTokenCount(usage, "prompt_tokens_details", "cached_tokens")
                ?? GetTokenCount(usage, "prompt_cache_hit_tokens"),
            ReasoningTokens: GetNestedTokenCount(
                usage,
                "completion_tokens_details",
                "reasoning_tokens"),
            Model: root["model"]?.GetValue<string>());
    }

    /// <summary>解析 Responses 流 response.completed 的 usage；无 usage 返回 null（静默）。</summary>
    private static AiUsageUpdate? TryCreateResponsesUsage(JsonObject response)
    {
        if (response["usage"] is not JsonObject usage)
        {
            return null;
        }

        return new AiUsageUpdate(
            GetTokenCount(usage, "input_tokens") ?? 0,
            GetTokenCount(usage, "output_tokens") ?? 0,
            GetNestedTokenCount(usage, "input_tokens_details", "cached_tokens"),
            ReasoningTokens: GetNestedTokenCount(
                usage,
                "output_tokens_details",
                "reasoning_tokens"),
            Model: response["model"]?.GetValue<string>());
    }

    /// <summary>解析 Ollama 最终 chunk 的 prompt_eval_count/eval_count；无计数返回 null（静默）。</summary>
    private static AiUsageUpdate? TryCreateOllamaUsage(JsonObject root)
    {
        long? promptEvalCount = GetTokenCount(root, "prompt_eval_count");
        long? evalCount = GetTokenCount(root, "eval_count");
        return promptEvalCount is null && evalCount is null
            ? null
            : new AiUsageUpdate(
                promptEvalCount ?? 0,
                evalCount ?? 0,
                Model: root["model"]?.GetValue<string>());
    }

    private static JsonObject CreateAssistantToolMessage(
        IReadOnlyList<ToolCall> calls,
        string? reasoningContent = null)
    {
        var toolCalls = new JsonArray();
        foreach (ToolCall call in calls)
        {
            toolCalls.Add(new JsonObject
            {
                ["id"] = call.Id,
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = call.Name,
                    ["arguments"] = call.Arguments.GetRawText(),
                },
            });
        }

        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = string.Empty,
            ["tool_calls"] = toolCalls,
        };
        if (!string.IsNullOrEmpty(reasoningContent))
        {
            // DeepSeek 等思考模型要求工具轮次回传 reasoning_content，否则 400。
            message["reasoning_content"] = reasoningContent;
        }

        return message;
    }

    private static JsonArray CreateAnthropicToolUseContent(IReadOnlyList<ToolCall> calls)
    {
        var content = new JsonArray();
        foreach (ToolCall call in calls)
        {
            content.Add(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = call.Id,
                ["name"] = call.Name,
                ["input"] = JsonNode.Parse(call.Arguments.GetRawText()),
            });
        }

        return content;
    }

    private static void AddCompletedResponseCalls(
        JsonObject response,
        Dictionary<string, StreamingToolCall> calls)
    {
        if (response["output"] is not JsonArray output)
        {
            return;
        }

        foreach (JsonObject item in output.OfType<JsonObject>().Where(
                     item => item["type"]?.GetValue<string>() == "function_call"))
        {
            string key = item["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
            if (calls.TryGetValue(key, out StreamingToolCall? existing)
                && existing.HasArguments)
            {
                continue;
            }

            var call = existing ?? new StreamingToolCall();
            call.Id ??= item["call_id"]?.GetValue<string>();
            call.Name ??= item["name"]?.GetValue<string>();
            call.AppendArguments(item["arguments"]?.GetValue<string>());
            calls[key] = call;
        }
    }

    private sealed class StreamingToolCall
    {
        private readonly StringBuilder arguments = new();

        public string? Id { get; set; }

        public string? Name { get; set; }

        public bool HasArguments => arguments.Length > 0;

        public void AppendArguments(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                arguments.Append(value);
            }
        }

        public ToolCall Build()
        {
            string id = Id ?? Guid.NewGuid().ToString("N");
            string name = Name
                ?? throw new InvalidDataException("流式工具调用缺少名称。");
            using JsonDocument document = JsonDocument.Parse(
                arguments.Length == 0 ? "{}" : arguments.ToString());
            return new ToolCall(id, name, document.RootElement.Clone());
        }
    }

    private sealed record StreamedChatResponse(
        IReadOnlyList<ToolCall> ToolCalls,
        string? ReasoningContent = null);

    private sealed record StreamedResponsesResponse(
        string? ResponseId,
        IReadOnlyList<ToolCall> ToolCalls);

    private sealed record StreamedAnthropicResponse(IReadOnlyList<ToolCall> ToolCalls);
}
