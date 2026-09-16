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
    private const int MaximumToolRounds = 4;
    private readonly Func<AiProviderSettings> getSettings;
    private readonly HttpClient httpClient;
    private readonly SqliteReadOnlyAiToolExecutor toolExecutor;
    private readonly ISecretStore secretStore;
    private readonly TimeProvider timeProvider;
    private readonly ReadOnlyAiToolCatalog toolCatalog = new();
    private readonly WriteAiToolCatalog writeToolCatalog = new();

    public ProviderAiAssistantClient(
        HttpClient httpClient,
        Func<AiProviderSettings> getSettings,
        ISecretStore secretStore,
        SqliteReadOnlyAiToolExecutor toolExecutor,
        TimeProvider? timeProvider = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
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

        IReadOnlyList<AiToolDefinition> tools = request.Mode == AiAssistantMode.Write
            ? [.. toolCatalog.Definitions, .. writeToolCatalog.Definitions]
            : toolCatalog.Definitions;
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
                settings, endpoint, apiKey!, context, request.Mode, proposals, cancellationToken),
            AiProviderKind.Anthropic => await SendAnthropicAsync(
                settings, endpoint, apiKey!, context, request.Mode, proposals, cancellationToken),
            AiProviderKind.OpenAiCompatible or AiProviderKind.Ollama => await SendChatAsync(
                settings, endpoint, apiKey, context, request.Mode, proposals, cancellationToken),
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
        AiAssistantMode mode,
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

        for (int round = 0; round < MaximumToolRounds; round++)
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
                string result = await ExecuteToolAsync(
                    toolCall, mode, proposals, cancellationToken);
                messages.Add(CreateChatToolMessage(settings.Provider, toolCall, result));
            }
        }

        throw new InvalidOperationException("模型工具调用次数超过限制。");
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
        AiAssistantMode mode,
        List<AiAssistantProposal> proposals,
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
                string result = await ExecuteToolAsync(
                    toolCall, mode, proposals, cancellationToken);
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

    private async Task<string> SendAnthropicAsync(
        AiProviderSettings settings,
        Uri endpoint,
        string apiKey,
        AiRequestContext context,
        AiAssistantMode mode,
        List<AiAssistantProposal> proposals,
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
                string result = await ExecuteToolAsync(
                    toolCall, mode, proposals, cancellationToken);
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

    private async Task<string> ExecuteToolAsync(
        ToolCall toolCall,
        AiAssistantMode mode,
        List<AiAssistantProposal> proposals,
        CancellationToken cancellationToken)
    {
        if (!toolCall.Name.StartsWith("propose_", StringComparison.Ordinal))
        {
            AiToolExecutionResult result = await toolExecutor.ExecuteAsync(
                toolCall.Name,
                toolCall.Arguments,
                cancellationToken);
            return result.Json;
        }

        if (mode != AiAssistantMode.Write)
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
