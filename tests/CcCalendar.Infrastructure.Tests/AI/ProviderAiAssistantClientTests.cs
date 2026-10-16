using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Security;
using CcCalendar.Core.Todos;
using CcCalendar.Infrastructure.AI;
using CcCalendar.Infrastructure.Tests.Persistence;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class ProviderAiAssistantClientTests
{
    [Fact]
    public async Task OpenAiChatStreamsTextDeltasFromSse()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"choices":[{"delta":{"content":"找到"}}]}

            data: {"choices":[{"delta":{"content":"发布待办。"}}]}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var client = CreateClient(database, handler, settings);
        var deltas = new List<string>();

        await client.StreamAsync(
            new AiAssistantRequest("查找发布待办", AiAssistantMode.ReadOnly),
            (update, _) =>
            {
                if (update is AiTextDelta delta)
                {
                    deltas.Add(delta.Text);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(["找到", "发布待办。"], deltas);
        Assert.Contains("\"stream\":true", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamingEmitsContextCompositionBeforeDeltas()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"choices":[{"delta":{"content":"好的"}}]}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var client = CreateClient(database, handler, settings);
        var updates = new List<AiAssistantUpdate>();

        await client.StreamAsync(
            new AiAssistantRequest(
                "查找发布待办",
                AiAssistantMode.ReadOnly,
                [new AiAssistantTurn(false, "上一轮回答")]),
            (update, _) =>
            {
                updates.Add(update);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // P74-02：构成事件必须是首个 update，随后才是文本分片。
        Assert.IsType<AiContextCompositionUpdate>(updates[0]);
        var composition = (AiContextCompositionUpdate)updates[0];
        Assert.True(composition.SystemTokens > 0);
        Assert.True(composition.ToolSchemaTokens > 0);
        Assert.Equal(
            AiTokenEstimator.EstimateTokens("查找发布待办"),
            composition.UserTokens);
        Assert.Equal(
            AiTokenEstimator.EstimateTokens("上一轮回答"),
            composition.AssistantTokens);
        Assert.Contains(updates, update => update is AiTextDelta { Text: "好的" });
    }

    [Fact]
    public async Task FirstNetworkChunkIsPublishedBeforeStreamCompletes()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new GatedStreamingHttpMessageHandler(
            "data: {\"choices\":[{\"delta\":{\"content\":\"第一段\"}}]}\n\n",
            "data: {\"choices\":[{\"delta\":{\"content\":\"第二段\"}}]}\n\n"
                + "data: [DONE]\n\n");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var firstDelta = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var deltas = new List<string>();

        Task streaming = CreateClient(database, handler, settings).StreamAsync(
            new AiAssistantRequest("测试流式", AiAssistantMode.ReadOnly),
            (update, _) =>
            {
                if (update is AiTextDelta delta)
                {
                    deltas.Add(delta.Text);
                    firstDelta.TrySetResult(delta.Text);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal("第一段", await firstDelta.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(streaming.IsCompleted);
        handler.ReleaseSecondChunk();
        await streaming;
        Assert.Equal(["第一段", "第二段"], deltas);
    }

    [Fact]
    public async Task OpenAiResponsesStreamsTextDeltasFromSse()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            event: response.output_text.delta
            data: {"type":"response.output_text.delta","delta":"找到"}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","delta":"发布待办。"}

            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_1","output":[]}}

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiResponses,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        Assert.Equal(
            ["找到", "发布待办。"],
            await StreamTextAsync(CreateClient(database, handler, settings)));
    }

    [Fact]
    public async Task OpenAiCompatibleStreamsReasoningDeltasWithoutChangingRequestMode()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [
                """
                data: {"choices":[{"delta":{"reasoning_content":"先分析"}}]}

                data: {"choices":[{"delta":{"content":"结论"}}]}

                data: [DONE]

                """,
            ],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.deepseek.test/v1",
            Model = "deepseek-test",
        };
        var reasoning = new List<string>();

        await CreateClient(database, handler, settings).StreamAsync(
            new AiAssistantRequest("查找发布待办", AiAssistantMode.ReadOnly),
            (update, _) =>
            {
                if (update is AiReasoningDelta delta)
                {
                    reasoning.Add(delta.Text);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(["先分析"], reasoning);
    }

    [Fact]
    public async Task AnthropicStreamsTextDeltasFromSse()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_1"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"找到"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"发布待办。"}}

            event: message_stop
            data: {"type":"message_stop"}

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Anthropic,
            Endpoint = "https://api.anthropic.test/v1",
            Model = "claude-test",
        };

        Assert.Equal(
            ["找到", "发布待办。"],
            await StreamTextAsync(CreateClient(database, handler, settings)));
    }

    [Fact]
    public async Task OllamaStreamsTextDeltasFromNdjson()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            {"message":{"role":"assistant","content":"找到"},"done":false}
            {"message":{"role":"assistant","content":"发布待办。"},"done":false}
            {"message":{"role":"assistant","content":""},"done":true}
            """],
            "application/x-ndjson");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            Endpoint = "http://localhost:11434",
            Model = "qwen-test",
            LocalOnlyMode = true,
        };

        Assert.Equal(
            ["找到", "发布待办。"],
            await StreamTextAsync(CreateClient(database, handler, settings)));
    }

    [Fact]
    public async Task OpenAiChatStreamEmitsUsageWithCachedAndReasoningTokens()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"choices":[{"delta":{"content":"找到"}}]}

            data: {"model":"gpt-test","choices":[],"usage":{"prompt_tokens":100,"completion_tokens":40,"prompt_tokens_details":{"cached_tokens":60},"completion_tokens_details":{"reasoning_tokens":12}}}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        AiUsageUpdate usage = Assert.Single(
            await StreamUsageAsync(CreateClient(database, handler, settings)));

        Assert.Equal(100, usage.PromptTokens);
        Assert.Equal(40, usage.CompletionTokens);
        Assert.Equal(60, usage.CachedPromptTokens);
        Assert.Null(usage.CacheWriteTokens);
        Assert.Equal(12, usage.ReasoningTokens);
        Assert.Equal("gpt-test", usage.Model);
        Assert.Contains(
            "\"stream_options\":{\"include_usage\":true}",
            handler.Requests[0].Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeepSeekStreamUsageMapsPromptCacheHitTokens()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"model":"deepseek-chat","choices":[],"usage":{"prompt_tokens":210,"completion_tokens":35,"prompt_cache_hit_tokens":192}}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.deepseek.test/v1",
            Model = "deepseek-chat",
        };

        AiUsageUpdate usage = Assert.Single(
            await StreamUsageAsync(CreateClient(database, handler, settings)));

        Assert.Equal(210, usage.PromptTokens);
        Assert.Equal(35, usage.CompletionTokens);
        Assert.Equal(192, usage.CachedPromptTokens);
        Assert.Null(usage.ReasoningTokens);
        Assert.Contains(
            "\"stream_options\":{\"include_usage\":true}",
            handler.Requests[0].Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiChatStreamWithoutUsageStaysSilent()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"choices":[{"delta":{"content":"找到"}}]}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        Assert.Empty(await StreamUsageAsync(CreateClient(database, handler, settings)));
        // 网关忽略 include_usage 时请求仍带 stream_options，只是流里不回 usage。
        Assert.Contains(
            "\"stream_options\":{\"include_usage\":true}",
            handler.Requests[0].Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiChatToolLoopEmitsUsagePerRound()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [
                """
                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"query_todos","arguments":"{\"query\":\"release\",\"limit\":5}"}}]}}]}

                data: {"choices":[],"usage":{"prompt_tokens":100,"completion_tokens":10}}

                data: [DONE]

                """,
                """
                data: {"choices":[{"delta":{"content":"找到发布待办。"}}]}

                data: {"choices":[],"usage":{"prompt_tokens":80,"completion_tokens":20,"prompt_cache_hit_tokens":50}}

                data: [DONE]

                """,
            ],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        List<AiUsageUpdate> usage = await StreamUsageAsync(
            CreateClient(database, handler, settings));

        Assert.Equal(2, usage.Count);
        Assert.Equal(10, usage[0].CompletionTokens);
        Assert.Null(usage[0].CachedPromptTokens);
        Assert.Equal(50, usage[1].CachedPromptTokens);
    }

    /// <summary>
    /// P70-05：工具循环对每次工具调用发 Running→Ok 事件（带参数/结果摘要），
    /// 事件先于下一轮请求发出。
    /// </summary>
    [Fact]
    public async Task OpenAiChatToolLoopEmitsToolCallRunningThenOk()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [
                """
                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"query_todos","arguments":"{\"query\":\"release\",\"limit\":5}"}}]}}]}

                data: [DONE]

                """,
                """
                data: {"choices":[{"delta":{"content":"找到发布待办。"}}]}

                data: [DONE]

                """,
            ],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        List<AiToolCallUpdate> toolCalls = await StreamToolCallsAsync(
            CreateClient(database, handler, settings));

        Assert.Equal(2, toolCalls.Count);
        Assert.All(toolCalls, update => Assert.Equal("query_todos", update.ToolName));
        Assert.Equal(AiToolCallState.Running, toolCalls[0].State);
        // 参数摘要：前两个键值拼接，能认出模型在查什么。
        Assert.Contains("release", toolCalls[0].ArgsSummary, StringComparison.Ordinal);
        Assert.Null(toolCalls[0].ResultSummary);
        Assert.Equal(AiToolCallState.Ok, toolCalls[1].State);
        // 结果摘要：查询工具回 JSON，摘要非空（截断规则见 SummarizeToolResult）。
        Assert.False(string.IsNullOrWhiteSpace(toolCalls[1].ResultSummary));
    }

    /// <summary>
    /// P70-05：工具执行抛异常时发 Running→Error（异常消息作 ResultSummary），
    /// 且原异常原样上抛——既有错误处理流不被破坏。
    /// </summary>
    [Fact]
    public async Task ToolLoopEmitsErrorEventAndRethrowsWhenToolFails()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [
                """
                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"propose_todo","arguments":"{\"title\":\"幻觉调用\",\"dueAt\":null}"}}]}}]}

                data: [DONE]

                """,
            ],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        ProviderAiAssistantClient client = CreateClient(database, handler, settings);
        var toolCalls = new List<AiToolCallUpdate>();

        // 只读模式下模型幻觉调用写入工具：ExecuteToolAsync 抛 InvalidOperationException。
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.StreamAsync(
                new AiAssistantRequest("只读模式下的幻觉调用", AiAssistantMode.ReadOnly),
                (update, _) =>
                {
                    if (update is AiToolCallUpdate toolCall)
                    {
                        toolCalls.Add(toolCall);
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None));

        Assert.Equal("只读模式不允许写入提案工具。", exception.Message);
        Assert.Equal(2, toolCalls.Count);
        Assert.Equal(AiToolCallState.Running, toolCalls[0].State);
        Assert.Equal(AiToolCallState.Error, toolCalls[1].State);
        Assert.Equal("只读模式不允许写入提案工具。", toolCalls[1].ResultSummary);
    }

    [Fact]
    public async Task AnthropicStreamMergesUsageFromMessageStartAndDelta()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_1","model":"claude-test","usage":{"input_tokens":25,"cache_read_input_tokens":10,"cache_creation_input_tokens":5,"output_tokens":2}}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"找到"}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":37}}

            event: message_stop
            data: {"type":"message_stop"}

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Anthropic,
            Endpoint = "https://api.anthropic.test/v1",
            Model = "claude-test",
        };

        AiUsageUpdate usage = Assert.Single(
            await StreamUsageAsync(CreateClient(database, handler, settings)));

        Assert.Equal(25, usage.PromptTokens);
        Assert.Equal(37, usage.CompletionTokens);
        Assert.Equal(10, usage.CachedPromptTokens);
        Assert.Equal(5, usage.CacheWriteTokens);
        Assert.Equal("claude-test", usage.Model);
    }

    [Fact]
    public async Task AnthropicStreamWithoutUsageStaysSilent()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_1"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"找到"}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"}}

            event: message_stop
            data: {"type":"message_stop"}

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Anthropic,
            Endpoint = "https://api.anthropic.test/v1",
            Model = "claude-test",
        };

        Assert.Empty(await StreamUsageAsync(CreateClient(database, handler, settings)));
    }

    [Fact]
    public async Task ResponsesCompletedUsageParsesTokenDetails()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            event: response.output_text.delta
            data: {"type":"response.output_text.delta","delta":"找到"}

            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_1","model":"gpt-test","output":[],"usage":{"input_tokens":80,"output_tokens":30,"input_tokens_details":{"cached_tokens":50},"output_tokens_details":{"reasoning_tokens":7}}}}

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiResponses,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        AiUsageUpdate usage = Assert.Single(
            await StreamUsageAsync(CreateClient(database, handler, settings)));

        Assert.Equal(80, usage.PromptTokens);
        Assert.Equal(30, usage.CompletionTokens);
        Assert.Equal(50, usage.CachedPromptTokens);
        Assert.Equal(7, usage.ReasoningTokens);
        Assert.Equal("gpt-test", usage.Model);
    }

    [Fact]
    public async Task ResponsesCompletedWithoutUsageStaysSilent()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_1","output":[]}}

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiResponses,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        Assert.Empty(await StreamUsageAsync(CreateClient(database, handler, settings)));
    }

    [Fact]
    public async Task OllamaStreamEmitsFinalChunkCounts()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            {"model":"qwen-test","message":{"role":"assistant","content":"找到"},"done":false}
            {"model":"qwen-test","message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":26,"eval_count":298}
            """],
            "application/x-ndjson");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            Endpoint = "http://localhost:11434",
            Model = "qwen-test",
            LocalOnlyMode = true,
        };

        AiUsageUpdate usage = Assert.Single(
            await StreamUsageAsync(CreateClient(database, handler, settings)));

        Assert.Equal(26, usage.PromptTokens);
        Assert.Equal(298, usage.CompletionTokens);
        Assert.Null(usage.CachedPromptTokens);
        Assert.Equal("qwen-test", usage.Model);
    }

    [Fact]
    public async Task OllamaStreamWithoutCountsStaysSilent()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            {"message":{"role":"assistant","content":"找到"},"done":false}
            {"message":{"role":"assistant","content":""},"done":true}
            """],
            "application/x-ndjson");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            Endpoint = "http://localhost:11434",
            Model = "qwen-test",
            LocalOnlyMode = true,
        };

        Assert.Empty(await StreamUsageAsync(CreateClient(database, handler, settings)));
    }

    [Fact]
    public async Task StreamedParallelToolCallsKeepIdPairingInFollowUpRequest()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string ProposalChunk(int index, string id, string date) =>
            $$$"""
            data: {"choices":[{"delta":{"tool_calls":[{"index":{{{index}}},"id":"{{{id}}}","type":"function","function":{"name":"propose_timed_event","arguments":""}}]}}]}

            data: {"choices":[{"delta":{"tool_calls":[{"index":{{{index}}},"function":{"arguments":"{\"title\":\"每日例会\",\"startAt\":\"{{{date}}}T18:10:00+08:00\",\"endAt\":\"{{{date}}}T19:10:00+08:00\",\"timeZoneId\":\"China Standard Time\"}"}}]}}]}

            """;
        string firstResponse = string.Join(
            "\n",
            """
            data: {"choices":[{"delta":{"content":null,"reasoning_content":"用户要四条日程"}}]}

            """,
            ProposalChunk(0, "call_00_aaaa", "2026-08-17"),
            ProposalChunk(1, "call_01_bbbb", "2026-08-18"),
            ProposalChunk(2, "call_02_cccc", "2026-08-19"),
            ProposalChunk(3, "call_03_dddd", "2026-08-20"),
            """
            data: {"choices":[{"delta":{"content":""},"finish_reason":"tool_calls"}]}

            data: [DONE]

            """);
        var handler = new QueueHttpMessageHandler(
            firstResponse,
            """
            data: {"choices":[{"delta":{"content":"已生成 4 条草案。"}}]}

            data: [DONE]

            """,
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.deepseek.test/v1",
            Model = "deepseek-v4-flash",
        };

        await CreateClient(database, handler, settings).StreamAsync(
            new AiAssistantRequest("本周一到周四18:10-19:10添加每日例会", AiAssistantMode.Write),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.True(handler.Requests.Count >= 2, "应发出第二轮工具结果请求。");
        JsonNode? followUp = JsonNode.Parse(handler.Requests[1].Body);
        var followUpMessages = followUp!["messages"]!.AsArray();
        var assistant = followUpMessages.FirstOrDefault(node =>
            node!["role"]!.GetValue<string>() == "assistant"
            && node["tool_calls"] is not null);
        Assert.NotNull(assistant);
        Assert.Equal(
            "用户要四条日程",
            assistant!["reasoning_content"]?.GetValue<string>());
        var echoedIds = assistant["tool_calls"]!.AsArray()
            .Select(call => call!["id"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(["call_00_aaaa", "call_01_bbbb", "call_02_cccc", "call_03_dddd"], echoedIds);
        string[] toolIds = followUpMessages
            .Where(node => node!["role"]!.GetValue<string>() == "tool")
            .Select(node => node!["tool_call_id"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(echoedIds, toolIds);
    }

    [Fact]
    public async Task StreamingToolArgumentsProduceProposalWithoutWritingDatabase()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [
                """
                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"propose_todo","arguments":"{\"title\":"}}]}}]}

                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"Streamed todo\",\"dueAt\":null}"}}]}}]}

                data: [DONE]

                """,
                """
                data: {"choices":[{"delta":{"content":"请确认。"}}]}

                data: [DONE]

                """,
            ],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var proposals = new List<AiAssistantProposal>();

        await CreateClient(database, handler, settings).StreamAsync(
            new AiAssistantRequest("创建待办", AiAssistantMode.Write),
            (update, _) =>
            {
                if (update is AiProposalUpdate proposal)
                {
                    proposals.Add(proposal.Proposal);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);

        AiCreationAssistantProposal created = Assert.IsType<AiCreationAssistantProposal>(
            Assert.Single(proposals));
        Assert.Equal("Streamed todo", created.Draft.Title);
        await using var verification = database.CreateContext();
        Assert.Equal(2, verification.Todos.Count());
    }

    [Fact]
    public async Task WriteModeReturnsCreationProposalWithoutWritingDatabase()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"choices":[{"message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","type":"function","function":{"name":"propose_todo","arguments":"{\"title\":\"Review release\",\"dueAt\":null}"}}]}}]}
            """,
            """
            {"choices":[{"message":{"role":"assistant","content":"已生成待办草案，请确认。"}}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var client = CreateClient(database, handler, settings);

        AiAssistantResult result = await client.SendAsync(
            new AiAssistantRequest("创建发布复核待办", AiAssistantMode.Write),
            CancellationToken.None);

        AiCreationAssistantProposal proposal = Assert.IsType<AiCreationAssistantProposal>(
            Assert.Single(result.Proposals));
        Assert.Equal("Review release", proposal.Draft.Title);
        Assert.Contains("\"name\":\"propose_todo\"", handler.Requests[0].Body, StringComparison.Ordinal);
        await using var verification = database.CreateContext();
        Assert.Equal(2, verification.Todos.Count());
    }

    [Fact]
    public async Task TimedEventProposalKeepsTitleLocationAndMeetingNumberSeparate()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"choices":[{"message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","type":"function","function":{"name":"propose_timed_event","arguments":"{\"title\":\"每日例会 | 腾讯会议 365-5683-5623 https://meeting.tencent.com/dm/0UbNon35iBc6\",\"startAt\":\"2026-08-24T18:10:00+08:00\",\"endAt\":\"2026-08-24T19:10:00+08:00\",\"timeZoneId\":\"China Standard Time\",\"location\":\"项目组二楼会议室\",\"meetingNumber\":\"365-5683-5623\"}"}}]}}]}
            """,
            """
            {"choices":[{"message":{"role":"assistant","content":"请确认。"}}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        AiAssistantResult result = await CreateClient(database, handler, settings).SendAsync(
            new AiAssistantRequest("创建每日例会", AiAssistantMode.Write),
            CancellationToken.None);

        AiCreationDraft draft = Assert.IsType<AiCreationAssistantProposal>(
            Assert.Single(result.Proposals)).Draft;
        Assert.Equal("每日例会", draft.Title);
        Assert.Equal("项目组二楼会议室", draft.Location);
        Assert.Equal("365-5683-5623", draft.MeetingNumber);
        Assert.Contains("\"location\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"meetingNumber\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimedEventProposalReanchorsUtcSuffixedTimesToStatedTimeZone()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"choices":[{"message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","type":"function","function":{"name":"propose_timed_event","arguments":"{\"title\":\"每日例会\",\"startAt\":\"2026-08-24T10:10:00Z\",\"endAt\":\"2026-08-24T11:10:00Z\",\"timeZoneId\":\"Asia/Shanghai\"}"}}]}}]}
            """,
            """
            {"choices":[{"message":{"role":"assistant","content":"请确认。"}}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        AiAssistantResult result = await CreateClient(database, handler, settings).SendAsync(
            new AiAssistantRequest("创建每日例会", AiAssistantMode.Write),
            CancellationToken.None);

        AiCreationDraft draft = Assert.IsType<AiCreationAssistantProposal>(
            Assert.Single(result.Proposals)).Draft;
        // 模型常把用户本地墙钟时间（10:10）误标成 Z 后缀；声明的时区是 Asia/Shanghai，
        // 应按该时区重新锚定为 02:10 UTC，而不是整段偏移 8 小时。
        Assert.Equal(
            DateTimeOffset.Parse("2026-08-24T02:10:00Z", CultureInfo.InvariantCulture),
            draft.StartAt);
        Assert.Equal(
            DateTimeOffset.Parse("2026-08-24T03:10:00Z", CultureInfo.InvariantCulture),
            draft.EndAt);
    }

    [Fact]
    public async Task ToolRoundRetriesWithSanitizedEchoOnBadRequest()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new StatusQueueHttpMessageHandler(
            (HttpStatusCode.OK,
                """
                {"choices":[{"message":{"role":"assistant","content":"","reasoning_content":"思考过程","tool_calls":[{"id":"call_1","type":"function","function":{"name":"propose_todo","arguments":"{\"title\":\"Review\",\"dueAt\":null}"}}]}}]}
                """),
            (HttpStatusCode.BadRequest,
                """
                {"error":{"message":"No tool call found for tool output with call_id call_1","type":"invalid_request_error"}}
                """),
            (HttpStatusCode.OK,
                """
                {"choices":[{"message":{"role":"assistant","content":"已生成草案。"}}]}
                """));
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.deepseek.test/v1",
            Model = "deepseek-v4-flash",
        };
        var client = CreateClient(database, handler, settings);

        AiAssistantResult result = await client.SendAsync(
            new AiAssistantRequest("创建待办", AiAssistantMode.Write),
            CancellationToken.None);

        Assert.Single(result.Proposals);
        Assert.Equal(3, handler.Bodies.Count);
        Assert.Contains("reasoning_content", handler.Bodies[1], StringComparison.Ordinal);
        // 重试请求降级为最保守回传：无 reasoning_content、无空 content。
        Assert.DoesNotContain("reasoning_content", handler.Bodies[2], StringComparison.Ordinal);
        JsonArray retryMessages = JsonNode.Parse(handler.Bodies[2])!["messages"]!.AsArray();
        JsonObject? retryAssistant = retryMessages
            .SingleOrDefault(node => node!["tool_calls"] is not null)?
            .AsObject();
        Assert.NotNull(retryAssistant);
        Assert.False(retryAssistant.ContainsKey("content"));
        Assert.Contains("\"tool_call_id\":\"call_1\"", handler.Bodies[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatToolLoopPreservesReasoningContentWhenEchoingAssistantMessage()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"choices":[{"message":{"role":"assistant","content":"","reasoning_content":"思考过程","tool_calls":[{"id":"call_1","type":"function","function":{"name":"propose_todo","arguments":"{\"title\":\"Review release\",\"dueAt\":null}"}}]}}]}
            """,
            """
            {"choices":[{"message":{"role":"assistant","content":"已生成待办草案，请确认。"}}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.deepseek.test/v1",
            Model = "deepseek-reasoner",
        };
        var client = CreateClient(database, handler, settings);

        AiAssistantResult result = await client.SendAsync(
            new AiAssistantRequest("创建发布复核待办", AiAssistantMode.Write),
            CancellationToken.None);

        Assert.Single(result.Proposals);
        Assert.Contains(
            "reasoning_content",
            handler.Requests[1].Body,
            StringComparison.Ordinal);
        Assert.Contains("\"tool_call_id\":\"call_1\"", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatRequestEmbedsCurrentLocalDateFromTimeProvider()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"choices":[{"message":{"role":"assistant","content":"好的。"}}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.deepseek.test/v1",
            Model = "deepseek-v4-flash",
        };
        var client = CreateClient(
            database,
            handler,
            settings,
            new FrozenTimeProvider(new DateTimeOffset(2026, 8, 19, 18, 27, 0, TimeSpan.FromHours(8))));

        await client.SendAsync(
            new AiAssistantRequest("本周一到周四18:10-19:10添加每日例会", AiAssistantMode.Write),
            CancellationToken.None);

        Assert.Contains("2026-08-19", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("18:27", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadOnlyModeDoesNotExposeProposalTools()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"choices":[{"message":{"role":"assistant","content":"没有修改任何内容。"}}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var client = CreateClient(database, handler, settings);

        AiAssistantResult result = await client.SendAsync(
            new AiAssistantRequest("删除发布待办", AiAssistantMode.ReadOnly),
            CancellationToken.None);

        Assert.Empty(result.Proposals);
        Assert.DoesNotContain("propose_", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteModeParsesStatusAndDeleteProposals()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        Guid todoId = Guid.NewGuid();
        Guid recordId = Guid.NewGuid();
        string toolResponse = """
            {"choices":[{"message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","type":"function","function":{"name":"propose_todo_status","arguments":"{\"todoId\":\"TODO_ID\",\"status\":\"Completed\"}"}},{"id":"call_2","type":"function","function":{"name":"propose_delete","arguments":"{\"entityKind\":\"Record\",\"entityId\":\"RECORD_ID\"}"}}]}}]}
            """
            .Replace("TODO_ID", todoId.ToString(), StringComparison.Ordinal)
            .Replace("RECORD_ID", recordId.ToString(), StringComparison.Ordinal);
        var handler = new QueueHttpMessageHandler(
            toolResponse,
            """
            {"choices":[{"message":{"role":"assistant","content":"已生成两个变更草案，请确认。"}}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var client = CreateClient(database, handler, settings);

        AiAssistantResult result = await client.SendAsync(
            new AiAssistantRequest("完成待办并删除记录", AiAssistantMode.Write),
            CancellationToken.None);

        Assert.Collection(
            result.Proposals,
            proposal => Assert.IsType<AiTodoStatusChange>(
                Assert.Single(Assert.IsType<AiChangeAssistantProposal>(proposal).Draft.Changes)),
            proposal => Assert.IsType<AiRecycleChange>(
                Assert.Single(Assert.IsType<AiChangeAssistantProposal>(proposal).Draft.Changes)));
    }

    [Fact]
    public async Task OpenAiResponsesToolLoopUsesNativeResponseItems()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"id":"resp_1","output":[{"type":"function_call","id":"fc_1","call_id":"call_1","name":"query_todos","arguments":"{\"query\":\"release\",\"limit\":5}"}]}
            """,
            """
            {"id":"resp_2","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"找到发布待办。"}]}]}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiResponses,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-5-mini",
        };
        var client = new ProviderAiAssistantClient(
            new HttpClient(handler),
            () => settings,
            new StaticSecretStore("openai-secret"),
            new SqliteReadOnlyAiToolExecutor(database.DatabasePath));

        string response = await client.SendAsync("查找发布待办", CancellationToken.None);

        Assert.Equal("找到发布待办。", response);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("https://api.openai.test/v1/responses", request.Uri.AbsoluteUri);
            Assert.Equal("Bearer openai-secret", request.Authorization);
        });
        Assert.Contains("\"instructions\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"function\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"previous_response_id\":\"resp_1\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"function_call_output\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("Release checklist", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Private unrelated todo", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnthropicToolLoopUsesMessagesContentBlocksAndHeaders()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            """
            {"id":"msg_1","content":[{"type":"tool_use","id":"toolu_1","name":"query_todos","input":{"query":"release","limit":5}}],"stop_reason":"tool_use"}
            """,
            """
            {"id":"msg_2","content":[{"type":"text","text":"找到发布待办。"}],"stop_reason":"end_turn"}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Anthropic,
            Endpoint = "https://api.anthropic.test/v1",
            Model = "claude-sonnet-4-5",
        };
        var client = new ProviderAiAssistantClient(
            new HttpClient(handler),
            () => settings,
            new StaticSecretStore("anthropic-secret"),
            new SqliteReadOnlyAiToolExecutor(database.DatabasePath));

        string response = await client.SendAsync("查找发布待办", CancellationToken.None);

        Assert.Equal("找到发布待办。", response);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("https://api.anthropic.test/v1/messages", request.Uri.AbsoluteUri);
            Assert.Null(request.Authorization);
            Assert.Equal("anthropic-secret", request.ApiKey);
            Assert.Equal("2023-06-01", request.AnthropicVersion);
        });
        Assert.Contains("\"input_schema\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"tool_result\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"tool_use_id\":\"toolu_1\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("Release checklist", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OllamaToolLoopUsesLocalEndpointAndReturnsOnlyToolProjection()
    {
        await using var database = await CreateDatabaseWithTodosAsync();

        var handler = new QueueHttpMessageHandler(
            """
            {"message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"query_todos","arguments":{"query":"release","limit":5}}}]}}
            """,
            """
            {"message":{"role":"assistant","content":"找到发布待办。"}}
            """);
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            Endpoint = "http://localhost:11434",
            Model = "qwen3:8b",
            LocalOnlyMode = true,
        };
        var client = new ProviderAiAssistantClient(
            new HttpClient(handler),
            () => settings,
            new EmptySecretStore(),
            new SqliteReadOnlyAiToolExecutor(database.DatabasePath));

        string response = await client.SendAsync("查找发布待办", CancellationToken.None);

        Assert.Equal("找到发布待办。", response);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("http://localhost:11434/api/chat", request.Uri.AbsoluteUri);
            Assert.Null(request.Authorization);
        });
        Assert.Contains("Release checklist", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Private unrelated todo", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsyncSurfacesApiErrorBody()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            HttpStatusCode.BadRequest,
            """{"error":{"message":"Model Not Exist","type":"invalid_request_error"}}""");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateClient(database, handler, settings).SendAsync(
                new AiAssistantRequest("查找发布待办", AiAssistantMode.ReadOnly),
                CancellationToken.None));

        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Model Not Exist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamAsyncSurfacesApiErrorBody()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            HttpStatusCode.Unauthorized,
            """{"error":{"message":"Authentication Fails (no such key)","type":"authentication_error"}}""");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => StreamTextAsync(CreateClient(database, handler, settings)));

        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Authentication Fails", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalAgentModeIncludesCatalogFilteredByToolSwitches()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"choices":[{"delta":{"content":"好的"}}]}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
        {
            Enabled = true,
            AllowReadFile = true,
            AllowListDirectory = true,
            AllowSearchFiles = true,
            AllowWriteFile = false,
            AllowRunCommand = false,
        }).StreamAsync(
            new AiAssistantRequest("帮我看看本地文件", AiAssistantMode.LocalAgent),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        string body = handler.Requests[0].Body;
        // 本地代理 = 查询 + 提案 + 只读三件套；写入两件套被开关关掉不得进入目录。
        // 按工具定义的 "name":"..." 键断言——系统指令文本里也提到工具名，
        // 直接 Contains 工具名会误伤。
        Assert.Contains("\"name\":\"query_events\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"propose_timed_event\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"read_file\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"list_directory\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"search_files\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"write_file\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"run_command\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalAgentModeIncludesWriteToolsWhenSwitchesEnabled()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"choices":[{"delta":{"content":"好的"}}]}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
        {
            Enabled = true,
            AllowWriteFile = true,
            AllowRunCommand = true,
        }).StreamAsync(
            new AiAssistantRequest("帮我写文件", AiAssistantMode.LocalAgent),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        string body = handler.Requests[0].Body;
        Assert.Contains("\"name\":\"write_file\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"run_command\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalAgentModeRejectedWhenSettingMissingOrDisabled()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            ["""
            data: {"choices":[{"delta":{"content":"好的"}}]}

            data: [DONE]

            """],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        // 未接线（null）：宿主未提供本地代理设置。
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateClient(database, handler, settings).StreamAsync(
                new AiAssistantRequest("本地代理", AiAssistantMode.LocalAgent),
                (_, _) => Task.CompletedTask,
                CancellationToken.None));
        // 已接线但 Enabled=false：设置页未开启。
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = false,
            }).StreamAsync(
                new AiAssistantRequest("本地代理", AiAssistantMode.LocalAgent),
                (_, _) => Task.CompletedTask,
                CancellationToken.None));
    }

    [Fact]
    public async Task LocalAgentModeExecutesLocalToolAndFeedsResultBack()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-agent-round-" + Guid.NewGuid().ToString("N") + ".txt");
        // 内容用 ASCII 标记：System.Text.Json 默认 encoder 会把非 ASCII 转义为 \uXXXX，
        // 断言 body.Contains(中文) 会误判失败。
        await File.WriteAllTextAsync(tempFile, "local-agent-roundtrip-ok");
        try
        {
            // 参数里的 Windows 路径用正斜杠表示，避免 SSE JSON 双层转义出错；
            // .NET 文件 API 对正斜杠路径同样有效。SSE 行用 JsonNode 程序化构造，
            // 规避内插原始字符串的大括号转义计数问题。
            string forwardSlashPath = tempFile.Replace('\\', '/');
            string arguments = JsonSerializer.Serialize(
                new Dictionary<string, string> { ["path"] = forwardSlashPath });
            string firstData = new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["delta"] = new JsonObject
                        {
                            ["tool_calls"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["index"] = 0,
                                    ["id"] = "call_1",
                                    ["function"] = new JsonObject
                                    {
                                        ["name"] = "read_file",
                                        ["arguments"] = arguments,
                                    },
                                },
                            },
                        },
                    },
                },
            }.ToJsonString();
            var handler = new QueueHttpMessageHandler(
            [
                $"data: {firstData}\n\ndata: [DONE]\n\n",
                """
                data: {"choices":[{"delta":{"content":"读到了。"}}]}

                data: [DONE]

                """,
            ],
            "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var toolCalls = new List<AiToolCallUpdate>();

            await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
            }).StreamAsync(
                new AiAssistantRequest("读取该文件", AiAssistantMode.LocalAgent),
                (update, _) =>
                {
                    if (update is AiToolCallUpdate toolCall)
                    {
                        toolCalls.Add(toolCall);
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None);

            // 工具行事件：Running → Ok（P70-05 通道，本地工具走同一链路）。
            Assert.Equal(
                [AiToolCallState.Running, AiToolCallState.Ok],
                toolCalls.Select(toolCall => toolCall.State));
            // 工具结果回传给模型的第二轮请求里应包含读到的内容。
            Assert.Contains("local-agent-roundtrip-ok", handler.Requests[1].Body, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LocalAgentModeAllowsProposalTools()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [
                """
                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"propose_todo","arguments":"{\"title\":\"本地代理提案\",\"dueAt\":null}"}}]}}]}

                data: [DONE]

                """,
                """
                data: {"choices":[{"delta":{"content":"已生成提案。"}}]}

                data: [DONE]

                """,
            ],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var proposals = new List<AiAssistantProposal>();

        await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
        {
            Enabled = true,
        }).StreamAsync(
            new AiAssistantRequest("建个待办", AiAssistantMode.LocalAgent),
            (update, _) =>
            {
                if (update is AiProposalUpdate proposal)
                {
                    proposals.Add(proposal.Proposal);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);

        AiCreationAssistantProposal creation = Assert.IsType<AiCreationAssistantProposal>(
            Assert.Single(proposals));
        Assert.Equal("本地代理提案", creation.Draft.Title);
    }

    [Fact]
    public async Task LocalAgentWriteToolExecutesAfterApprovalAllowOnce()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-approval-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var handler = new QueueHttpMessageHandler(
                [CreateWriteFileSse(tempFile, "approval-roundtrip-ok"), CreateTextSse("written")],
                "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var toolCalls = new List<AiToolCallUpdate>();
            string? approvedTool = null;

            await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
                AllowWriteFile = true,
            }).StreamAsync(
                new AiAssistantRequest(
                    "写文件",
                    AiAssistantMode.LocalAgent,
                    toolApproval: (approval, _) =>
                    {
                        approvedTool = approval.ToolName;
                        return Task.FromResult(AiToolApprovalDecision.AllowOnce);
                    }),
                (update, _) =>
                {
                    if (update is AiToolCallUpdate toolCall)
                    {
                        toolCalls.Add(toolCall);
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None);

            // 审批回调收到工具名；事件序：先审（PendingApproval）后跑（Running → Ok）。
            Assert.Equal("write_file", approvedTool);
            Assert.Equal(
                [AiToolCallState.PendingApproval, AiToolCallState.Running, AiToolCallState.Ok],
                toolCalls.Select(toolCall => toolCall.State));
            Assert.True(File.Exists(tempFile));
            Assert.Equal("approval-roundtrip-ok", await File.ReadAllTextAsync(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LocalAgentWriteToolDeniedByUserDecision()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-approval-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var handler = new QueueHttpMessageHandler(
                [CreateWriteFileSse(tempFile, "denied-marker"), CreateTextSse("ok")],
                "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var toolCalls = new List<AiToolCallUpdate>();

            await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
                AllowWriteFile = true,
            }).StreamAsync(
                new AiAssistantRequest(
                    "写文件",
                    AiAssistantMode.LocalAgent,
                    toolApproval: (_, _) => Task.FromResult(AiToolApprovalDecision.Deny)),
                (update, _) =>
                {
                    if (update is AiToolCallUpdate toolCall)
                    {
                        toolCalls.Add(toolCall);
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None);

            // 拒绝：PendingApproval → Error，文件未写，error 结果回传模型继续对话。
            Assert.Equal(
                [AiToolCallState.PendingApproval, AiToolCallState.Error],
                toolCalls.Select(toolCall => toolCall.State));
            Assert.Equal("用户已拒绝", toolCalls[1].ResultSummary);
            Assert.False(File.Exists(tempFile));
            Assert.Equal(2, handler.Requests.Count);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LocalAgentWriteToolDeniedWhenApprovalCallbackMissing()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-approval-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var handler = new QueueHttpMessageHandler(
                [CreateWriteFileSse(tempFile, "no-callback-marker"), CreateTextSse("ok")],
                "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var toolCalls = new List<AiToolCallUpdate>();

            // 未接审批回调（测试宿主/旧调用方）：安全默认拒绝，不静默执行写操作。
            await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
                AllowWriteFile = true,
            }).StreamAsync(
                new AiAssistantRequest("写文件", AiAssistantMode.LocalAgent),
                (update, _) =>
                {
                    if (update is AiToolCallUpdate toolCall)
                    {
                        toolCalls.Add(toolCall);
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Equal(
                [AiToolCallState.PendingApproval, AiToolCallState.Error],
                toolCalls.Select(toolCall => toolCall.State));
            Assert.False(File.Exists(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LocalAgentAlwaysAllowedToolSkipsApproval()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-approval-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var handler = new QueueHttpMessageHandler(
                [CreateWriteFileSse(tempFile, "always-allowed-marker"), CreateTextSse("ok")],
                "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var toolCalls = new List<AiToolCallUpdate>();
            bool approvalInvoked = false;

            await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
                AllowWriteFile = true,
                AlwaysAllowTools = ["write_file"],
            }).StreamAsync(
                new AiAssistantRequest(
                    "写文件",
                    AiAssistantMode.LocalAgent,
                    toolApproval: (_, _) =>
                    {
                        approvalInvoked = true;
                        return Task.FromResult(AiToolApprovalDecision.AllowOnce);
                    }),
                (update, _) =>
                {
                    if (update is AiToolCallUpdate toolCall)
                    {
                        toolCalls.Add(toolCall);
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None);

            // 白名单命中：不弹审批（无 PendingApproval 事件、回调不触发），直接执行。
            Assert.False(approvalInvoked);
            Assert.Equal(
                [AiToolCallState.Running, AiToolCallState.Ok],
                toolCalls.Select(toolCall => toolCall.State));
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LocalAgentToolApprovalTimeoutDeniesByDefault()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-approval-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var handler = new QueueHttpMessageHandler(
                [CreateWriteFileSse(tempFile, "timeout-marker"), CreateTextSse("ok")],
                "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var toolCalls = new List<AiToolCallUpdate>();

            await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
                AllowWriteFile = true,
            }).StreamAsync(
                // 回调永不完成 + 注入短超时：等待期用户无响应，默认拒绝。
                new AiAssistantRequest(
                    "写文件",
                    AiAssistantMode.LocalAgent,
                    toolApproval: (_, _) => new TaskCompletionSource<AiToolApprovalDecision>().Task)
                {
                    ApprovalTimeout = TimeSpan.FromMilliseconds(100),
                },
                (update, _) =>
                {
                    if (update is AiToolCallUpdate toolCall)
                    {
                        toolCalls.Add(toolCall);
                    }

                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Equal(
                [AiToolCallState.PendingApproval, AiToolCallState.Error],
                toolCalls.Select(toolCall => toolCall.State));
            Assert.Equal("用户已拒绝", toolCalls[1].ResultSummary);
            Assert.False(File.Exists(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LocalAgentToolApprovalStopPropagatesCancellation()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-approval-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            using var cts = new CancellationTokenSource();
            var handler = new QueueHttpMessageHandler(
                [CreateWriteFileSse(tempFile, "cancel-marker")],
                "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var toolCalls = new List<AiToolCallUpdate>();

            // 审批等待期间用户点停止：外层令牌取消，异常原样上抛（不发 Error 事件）。
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
                {
                    Enabled = true,
                    AllowWriteFile = true,
                }).StreamAsync(
                    new AiAssistantRequest(
                        "写文件",
                        AiAssistantMode.LocalAgent,
                        toolApproval: (_, _) =>
                        {
                            cts.Cancel();
                            return new TaskCompletionSource<AiToolApprovalDecision>().Task;
                        }),
                    (update, _) =>
                    {
                        if (update is AiToolCallUpdate toolCall)
                        {
                            toolCalls.Add(toolCall);
                        }

                        return Task.CompletedTask;
                    },
                    cts.Token));

            Assert.Equal([AiToolCallState.PendingApproval], toolCalls.Select(toolCall => toolCall.State));
            Assert.False(File.Exists(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LocalAgentSpawnSubagentFeedsAnswerBackToParent()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [
                CreateSpawnSubagentSse("统计待办", "统计当前待办数量"),
                CreateTextSse("子代理答复：共 2 条"),
                CreateTextSse("完成"),
            ],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var events = new List<AiAssistantUpdate>();

        await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
        {
            Enabled = true,
        }).StreamAsync(
            new AiAssistantRequest("查待办", AiAssistantMode.LocalAgent),
            (update, _) =>
            {
                events.Add(update);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // 事件流：Started → 子代理文本 → Completed(true)；spawn 不落工具行事件。
        // P74-02 起子会话的上下文构成事件（AiContextCompositionUpdate）也会包进
        // 子代理流，验序前先滤掉。
        AiSubagentUpdate[] subagentEvents = [..
            events.OfType<AiSubagentUpdate>()
                .Where(update => update.Inner is not AiContextCompositionUpdate)];
        Assert.IsType<AiSubagentStarted>(subagentEvents[0].Inner);
        Assert.IsType<AiTextDelta>(subagentEvents[1].Inner);
        AiSubagentCompleted completed = Assert.IsType<AiSubagentCompleted>(subagentEvents[^1].Inner);
        Assert.True(completed.Succeeded);
        Assert.Empty(events.OfType<AiToolCallUpdate>());
        // 请求序：主会话（spawn 调用）→ 子会话（task 作为 user message）→
        // 主会话第 2 轮（子代理答复作为工具结果回传）。
        // 注意：body 由 JsonNode.ToJsonString() 序列化，非 ASCII 默认转义成
        // \uXXXX（System.Text.Json 默认 encoder），断言须解析 JSON 比对解码值。
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(
            "统计当前待办数量",
            GetLastMessageContent(handler.Requests[1].Body, "user"));
        Assert.Equal(
            "子代理答复：共 2 条",
            GetLastMessageContent(handler.Requests[2].Body, "tool"));
    }

    /// <summary>取 body.messages 里最后一条指定 role 的 content（中文已被 \uXXXX 转义，须解码比对）。</summary>
    private static string? GetLastMessageContent(string body, string role)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Last(message => message.GetProperty("role").GetString() == role)
            .GetProperty("content")
            .GetString();
    }

    [Fact]
    public async Task LocalAgentSubagentDepthExcludesSpawnToolAtLimit()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new QueueHttpMessageHandler(
            [CreateTextSse("ok"), CreateTextSse("ok")],
            "text/event-stream");
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };

        // 主会话（depth=0）：目录含 spawn_subagent；达上限（depth=2）：不再注入。
        await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
        {
            Enabled = true,
        }).StreamAsync(
            new AiAssistantRequest("任务", AiAssistantMode.LocalAgent),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);
        await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
        {
            Enabled = true,
        }).StreamAsync(
            new AiAssistantRequest("任务", AiAssistantMode.LocalAgent) { SubagentDepth = 2 },
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.Contains("\"name\":\"spawn_subagent\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"spawn_subagent\"", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalAgentSubagentFailureEmitsCompletedAndRethrows()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        var handler = new StatusQueueHttpMessageHandler(
            (HttpStatusCode.OK, CreateSpawnSubagentSse("失败任务", "执行失败的任务")),
            (HttpStatusCode.InternalServerError, "{}"));
        var settings = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.test/v1",
            Model = "gpt-test",
        };
        var events = new List<AiAssistantUpdate>();

        // 子会话 HTTP 500：先发 Completed(false) 再原样上抛，主会话不继续。
        await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
            }).StreamAsync(
                new AiAssistantRequest("任务", AiAssistantMode.LocalAgent),
                (update, _) =>
                {
                    events.Add(update);
                    return Task.CompletedTask;
                },
                CancellationToken.None));

        AiSubagentUpdate[] subagentEvents = [.. events.OfType<AiSubagentUpdate>()];
        Assert.IsType<AiSubagentStarted>(subagentEvents[0].Inner);
        AiSubagentCompleted completed = Assert.IsType<AiSubagentCompleted>(subagentEvents[^1].Inner);
        Assert.False(completed.Succeeded);
        Assert.False(string.IsNullOrEmpty(completed.Error));
        Assert.Equal(2, handler.Bodies.Count);
    }

    [Fact]
    public async Task LocalAgentSubagentForwardsApprovalInsideSubsession()
    {
        await using var database = await CreateDatabaseWithTodosAsync();
        string tempFile = Path.Combine(Path.GetTempPath(), "cccalendar-subagent-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var handler = new QueueHttpMessageHandler(
                [
                    CreateSpawnSubagentSse("写报告", "把结论写进文件"),
                    CreateWriteFileSse(tempFile, "subagent-approval-ok"),
                    CreateTextSse("子代理完成"),
                    CreateTextSse("最终答复"),
                ],
                "text/event-stream");
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://api.openai.test/v1",
                Model = "gpt-test",
            };
            var events = new List<AiAssistantUpdate>();
            string? approvedTool = null;

            await CreateClient(database, handler, settings, getLocalAgentSettings: () => new LocalAgentSettings
            {
                Enabled = true,
                AllowWriteFile = true,
            }).StreamAsync(
                new AiAssistantRequest(
                    "写报告",
                    AiAssistantMode.LocalAgent,
                    toolApproval: (approval, _) =>
                    {
                        approvedTool = approval.ToolName;
                        return Task.FromResult(AiToolApprovalDecision.AllowOnce);
                    }),
                (update, _) =>
                {
                    events.Add(update);
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            // 审批回调被子会话透传触发；子代理内的工具事件全部带包装（同一 SubagentId）。
            Assert.Equal("write_file", approvedTool);
            AiToolCallUpdate[] innerToolCalls = [..
                events.OfType<AiSubagentUpdate>()
                    .Select(subagent => subagent.Inner)
                    .OfType<AiToolCallUpdate>()];
            Assert.Equal(
                [AiToolCallState.PendingApproval, AiToolCallState.Running, AiToolCallState.Ok],
                innerToolCalls.Select(toolCall => toolCall.State));
            Guid subagentId = events.OfType<AiSubagentUpdate>().First().SubagentId;
            Assert.All(events.OfType<AiSubagentUpdate>(), update => Assert.Equal(subagentId, update.SubagentId));
            Assert.True(File.Exists(tempFile));
            Assert.Equal(4, handler.Requests.Count);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>构造 spawn_subagent 工具调用的 SSE 首轮响应（JsonNode 程序化）。</summary>
    private static string CreateSpawnSubagentSse(string description, string task)
    {
        string arguments = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["description"] = description,
            ["task"] = task,
        });
        string firstData = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["id"] = "call_1",
                                ["function"] = new JsonObject
                                {
                                    ["name"] = "spawn_subagent",
                                    ["arguments"] = arguments,
                                },
                            },
                        },
                    },
                },
            },
        }.ToJsonString();
        return $"data: {firstData}\n\ndata: [DONE]\n\n";
    }

    /// <summary>构造 write_file 工具调用的 SSE 首轮响应（JsonNode 程序化，规避内插原始字符串转义计数）。</summary>
    private static string CreateWriteFileSse(string path, string content)
    {
        string arguments = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["path"] = path.Replace('\\', '/'),
            ["content"] = content,
        });
        string firstData = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["id"] = "call_1",
                                ["function"] = new JsonObject
                                {
                                    ["name"] = "write_file",
                                    ["arguments"] = arguments,
                                },
                            },
                        },
                    },
                },
            },
        }.ToJsonString();
        return $"data: {firstData}\n\ndata: [DONE]\n\n";
    }

    /// <summary>构造纯文本回复的 SSE 响应（工具轮之后的收尾轮）。</summary>
    private static string CreateTextSse(string text)
    {
        string data = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject { ["delta"] = new JsonObject { ["content"] = text } },
            },
        }.ToJsonString();
        return $"data: {data}\n\ndata: [DONE]\n\n";
    }

    private sealed class StatusQueueHttpMessageHandler(
        params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> pending = new(responses);

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            (HttpStatusCode status, string body) = pending.Dequeue();
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly string mediaType;
        private readonly Queue<string> responses;
        private HttpStatusCode statusCode = HttpStatusCode.OK;

        public QueueHttpMessageHandler(params string[] responses)
            : this(responses, "application/json")
        {
        }

        public QueueHttpMessageHandler(HttpStatusCode statusCode, params string[] responses)
            : this(responses)
        {
            this.statusCode = statusCode;
        }

        public QueueHttpMessageHandler(IEnumerable<string> responses, string mediaType)
        {
            this.responses = new Queue<string>(responses);
            this.mediaType = mediaType;
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("x-api-key", out IEnumerable<string>? apiKeys)
                    ? apiKeys.Single()
                    : null,
                request.Headers.TryGetValues("anthropic-version", out IEnumerable<string>? versions)
                    ? versions.Single()
                    : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, mediaType),
            };
        }
    }

    private static async Task<TemporaryCalendarDatabase> CreateDatabaseWithTodosAsync()
    {
        var database = new TemporaryCalendarDatabase();
        await database.InitializeAsync(CancellationToken.None);
        await using var seed = database.CreateContext();
        seed.Add(TodoItem.Create("Release checklist", null, null));
        seed.Add(TodoItem.Create("Private unrelated todo", null, null));
        await seed.SaveChangesAsync(CancellationToken.None);
        return database;
    }

    private static ProviderAiAssistantClient CreateClient(
        TemporaryCalendarDatabase database,
        HttpMessageHandler handler,
        AiProviderSettings settings,
        TimeProvider? timeProvider = null,
        Func<LocalAgentSettings>? getLocalAgentSettings = null)
    {
        return new ProviderAiAssistantClient(
            new HttpClient(handler),
            () => settings,
            new StaticSecretStore("test-secret"),
            new SqliteReadOnlyAiToolExecutor(database.DatabasePath),
            timeProvider,
            getLocalAgentSettings);
    }

    private sealed class FrozenTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => localNow.Offset == TimeSpan.Zero
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.CreateCustomTimeZone("Frozen", localNow.Offset, null, null);
    }

    private static async Task<IReadOnlyList<string>> StreamTextAsync(
        ProviderAiAssistantClient client)
    {
        var deltas = new List<string>();
        await client.StreamAsync(
            new AiAssistantRequest("查找发布待办", AiAssistantMode.ReadOnly),
            (update, _) =>
            {
                if (update is AiTextDelta delta)
                {
                    deltas.Add(delta.Text);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);
        return deltas;
    }

    private static async Task<List<AiUsageUpdate>> StreamUsageAsync(
        ProviderAiAssistantClient client)
    {
        var usage = new List<AiUsageUpdate>();
        await client.StreamAsync(
            new AiAssistantRequest("查找发布待办", AiAssistantMode.ReadOnly),
            (update, _) =>
            {
                if (update is AiUsageUpdate usageUpdate)
                {
                    usage.Add(usageUpdate);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);
        return usage;
    }

    // P70-05：收集工具调用可见性事件（Running/Ok/Error）。
    private static async Task<List<AiToolCallUpdate>> StreamToolCallsAsync(
        ProviderAiAssistantClient client)
    {
        var toolCalls = new List<AiToolCallUpdate>();
        await client.StreamAsync(
            new AiAssistantRequest("查找发布待办", AiAssistantMode.ReadOnly),
            (update, _) =>
            {
                if (update is AiToolCallUpdate toolCall)
                {
                    toolCalls.Add(toolCall);
                }

                return Task.CompletedTask;
            },
            CancellationToken.None);
        return toolCalls;
    }

    private sealed record CapturedRequest(
        Uri Uri,
        string? Authorization,
        string? ApiKey,
        string? AnthropicVersion,
        string Body);

    private sealed class StaticSecretStore(string secret) : ISecretStore
    {
        public Task<string?> ReadAsync(string identifier, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(secret);

        public Task WriteAsync(string identifier, string value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string identifier, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class EmptySecretStore : ISecretStore
    {
        public Task<string?> ReadAsync(string identifier, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task WriteAsync(string identifier, string secret, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string identifier, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class GatedStreamingHttpMessageHandler(
        string firstChunk,
        string secondChunk) : HttpMessageHandler
    {
        private readonly TaskCompletionSource release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseSecondChunk() => release.TrySetResult();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = new StreamContent(new GatedReadStream(
                Encoding.UTF8.GetBytes(firstChunk),
                Encoding.UTF8.GetBytes(secondChunk),
                release.Task));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
            });
        }
    }

    private sealed class GatedReadStream(
        byte[] firstChunk,
        byte[] secondChunk,
        Task release) : Stream
    {
        private int chunkIndex;
        private int offset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int bufferOffset, int count)
        {
            if (chunkIndex == 1)
            {
                release.GetAwaiter().GetResult();
            }

            return ReadAvailable(buffer.AsSpan(bufferOffset, count));
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (chunkIndex == 1)
            {
                await release.WaitAsync(cancellationToken);
            }

            return ReadAvailable(buffer.Span);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private int ReadAvailable(Span<byte> target)
        {
            byte[] source = chunkIndex == 0 ? firstChunk : secondChunk;
            if (chunkIndex > 1)
            {
                return 0;
            }

            int length = Math.Min(target.Length, source.Length - offset);
            source.AsSpan(offset, length).CopyTo(target);
            offset += length;
            if (offset == source.Length)
            {
                chunkIndex++;
                offset = 0;
            }

            return length;
        }
    }
}
