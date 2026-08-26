using System.Net;
using System.Text;
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
        TimeProvider? timeProvider = null)
    {
        return new ProviderAiAssistantClient(
            new HttpClient(handler),
            () => settings,
            new StaticSecretStore("test-secret"),
            new SqliteReadOnlyAiToolExecutor(database.DatabasePath),
            timeProvider);
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
