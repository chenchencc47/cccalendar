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

        IReadOnlyList<AiToolDefinition> tools = request.Mode == AiAssistantMode.Write
            ? [.. toolCatalog.Definitions, .. writeToolCatalog.Definitions]
            : toolCatalog.Definitions;
        AiRequestContext context = MinimalAiContextBuilder.Build(
            request.UserMessage,
            tools,
            request.Mode,
            timeProvider.GetLocalNow(),
            request.Conversation);
        switch (settings.Provider)
        {
            case AiProviderKind.OpenAiResponses:
                await StreamResponsesAsync(
                    settings, endpoint, apiKey!, context, request.Mode, onUpdate, cancellationToken);
                break;
            case AiProviderKind.Anthropic:
                await StreamAnthropicAsync(
                    settings, endpoint, apiKey!, context, request.Mode, onUpdate, cancellationToken);
                break;
            case AiProviderKind.OpenAiCompatible:
                await StreamOpenAiChatAsync(
                    settings, endpoint, apiKey, context, request.Mode, onUpdate, cancellationToken);
                break;
            case AiProviderKind.Ollama:
                await StreamOllamaAsync(
                    settings, endpoint, context, request.Mode, onUpdate, cancellationToken);
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
        AiAssistantMode mode,
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
        for (int round = 0; round < MaximumToolRounds; round++)
        {
            var body = new JsonObject
            {
                ["model"] = settings.Model,
                ["messages"] = messages.DeepClone(),
                ["tools"] = tools.DeepClone(),
                ["stream"] = true,
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
                    toolCall, mode, onUpdate, cancellationToken);
                messages.Add(CreateChatToolMessage(settings.Provider, toolCall, result));
            }
        }

        throw new InvalidOperationException("模型工具调用次数超过限制。");
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
                JsonObject? delta = root["choices"]?[0]?["delta"]?.AsObject();
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
        AiAssistantMode mode,
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
        for (int round = 0; round < MaximumToolRounds; round++)
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
                    toolCall, mode, onUpdate, cancellationToken);
                messages.Add(CreateChatToolMessage(settings.Provider, toolCall, result));
            }
        }

        throw new InvalidOperationException("模型工具调用次数超过限制。");
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
        AiAssistantMode mode,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        JsonArray input = CreateConversationMessages(context.Conversation, context.UserMessage);
        JsonArray tools = CreateOpenAiTools(context.Tools);
        string? previousResponseId = null;
        for (int round = 0; round < MaximumToolRounds; round++)
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
                    toolCall, mode, onUpdate, cancellationToken);
                input.Add(new JsonObject
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = toolCall.Id,
                    ["output"] = result,
                });
            }
        }

        throw new InvalidOperationException("模型工具调用次数超过限制。");
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
        AiAssistantMode mode,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray();
        AddConversationMessages(messages, context.Conversation);
        messages.Add(CreateMessage("user", context.UserMessage));
        JsonArray tools = CreateAnthropicTools(context.Tools);
        for (int round = 0; round < MaximumToolRounds; round++)
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
                    toolCall, mode, onUpdate, cancellationToken);
                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolCall.Id,
                    ["content"] = result,
                });
            }

            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }

        throw new InvalidOperationException("模型工具调用次数超过限制。");
    }

    private static async Task<StreamedAnthropicResponse> ReadAnthropicStreamAsync(
        StreamReader reader,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var calls = new Dictionary<int, StreamingToolCall>();
        await ReadSseAsync(
            reader,
            async (_, data, token) =>
            {
                JsonObject root = JsonNode.Parse(data)?.AsObject()
                    ?? throw new InvalidDataException("Anthropic 流返回了空事件。");
                string type = root["type"]?.GetValue<string>() ?? string.Empty;
                int index = root["index"]?.GetValue<int>() ?? 0;
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
        AiAssistantMode mode,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        var proposals = new List<AiAssistantProposal>();
        string result = await ExecuteToolAsync(toolCall, mode, proposals, cancellationToken);
        foreach (AiAssistantProposal proposal in proposals)
        {
            await onUpdate(new AiProposalUpdate(proposal), cancellationToken);
        }

        return result;
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
