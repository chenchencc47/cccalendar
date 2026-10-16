using System.IO;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.AI;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class AssistantViewModelTests
{
    [Fact]
    public async Task ContextCompositionFeedsMeterSegments()
    {
        var service = new FakeConfirmationService();
        var model = new AiModelConfiguration(
            "deepseek",
            "DeepSeek",
            new AiProviderSettings { Model = "deepseek-v4-flash" },
            ContextWindowTokens: 131072);
        var catalog = new AiModelCatalog([model], model.Id, model.Settings);
        var viewModel = new AssistantViewModel(
            service,
            assistantClient: new CompositionThenTextClient(
                new AiContextCompositionUpdate(100, 200, 300, 50)),
            modelCatalog: catalog)
        {
            InputText = "今天有什么待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        // P74-02：构成事件落到分段属性；窗口来自所选模型元数据。
        Assert.Equal(131072, viewModel.ContextWindowTokens);
        Assert.True(viewModel.HasContextComposition);
        Assert.Equal(650, viewModel.ContextUsedTokens);
        Assert.Equal(
            ["系统指令", "工具定义", "用户消息", "助手回复"],
            [.. viewModel.ContextSegments.Select(segment => segment.Name)]);
        Assert.Equal(
            [100, 200, 300, 50],
            [.. viewModel.ContextSegments.Select(segment => segment.Tokens)]);
    }

    [Fact]
    public async Task ContextMeterSummaryFormatsWindowUsageAndSegmentDetails()
    {
        var service = new FakeConfirmationService();
        var model = new AiModelConfiguration(
            "deepseek",
            "DeepSeek",
            new AiProviderSettings { Model = "deepseek-v4-flash" },
            ContextWindowTokens: 131072);
        var catalog = new AiModelCatalog([model], model.Id, model.Settings);
        var viewModel = new AssistantViewModel(
            service,
            assistantClient: new CompositionThenTextClient(
                new AiContextCompositionUpdate(1000, 2000, 3000, 500)),
            modelCatalog: catalog)
        {
            InputText = "今天有什么待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        // 有窗口：摘要含已用缩写、窗口缩写与占用百分比（6500/131072≈5%）。
        Assert.Contains("6.5k", viewModel.ContextMeterSummary, StringComparison.Ordinal);
        Assert.Contains("131.1k", viewModel.ContextMeterSummary, StringComparison.Ordinal);
        Assert.Contains("5%", viewModel.ContextMeterSummary, StringComparison.Ordinal);

        // 段详情：tokens 缩写 + 占已用百分比（工具段 2000/6500≈31%）。
        AssistantViewModel.ContextSegmentInfo tools =
            viewModel.ContextSegments.Single(segment => segment.Key == "tools");
        Assert.Contains("2k", tools.Detail, StringComparison.Ordinal);
        Assert.Contains("31%", tools.Detail, StringComparison.Ordinal);
        Assert.Matches("^#[0-9A-F]{6}$", tools.Color);
    }

    [Fact]
    public async Task ContextCompositionWithoutWindowStillShowsSegments()
    {
        var service = new FakeConfirmationService();
        var viewModel = new AssistantViewModel(
            service,
            assistantClient: new CompositionThenTextClient(
                new AiContextCompositionUpdate(100, 200, 300, 50)))
        {
            InputText = "今天有什么待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Null(viewModel.ContextWindowTokens);
        Assert.True(viewModel.HasContextComposition);
        Assert.Equal(4, viewModel.ContextSegments.Count);
    }

    [Fact]
    public async Task ImageAttachmentIsTranscribedIntoOutboundMessage()
    {
        var service = new FakeConfirmationService();
        var mainModel = new AiModelConfiguration(
            "deepseek",
            "DeepSeek",
            new AiProviderSettings { Model = "deepseek-v4-flash" });
        var visionModel = new AiModelConfiguration(
            "vision",
            "千问视觉",
            new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://dashscope.example/v1",
                Model = "qwen-vl-max",
            });
        var catalog = new AiModelCatalog([mainModel, visionModel], mainModel.Id, mainModel.Settings);
        var client = new CapturingAssistantClient();
        var transcriber = new StubVisionTranscriber(
            (_, _) => Task.FromResult("白板照片上列了三条待办"));
        var viewModel = new AssistantViewModel(
            service,
            assistantClient: client,
            modelCatalog: catalog,
            visionTranscriber: transcriber,
            getVisualBridgeModelId: () => visionModel.Id)
        {
            InputText = "整理图片里的待办",
        };
        viewModel.Attachments.Add(new AiTextAttachment(
            "board.png",
            @"C:\shots\board.png",
            1024,
            string.Empty,
            IsImage: true));

        await viewModel.SendAsync(CancellationToken.None);

        // 出站消息注入视觉转写文本，且带附件边界。
        Assert.Contains("白板照片上列了三条待办", client.LastMessage, StringComparison.Ordinal);
        Assert.Contains("board.png", client.LastMessage, StringComparison.Ordinal);
        // 转写器收到视觉桥模型的设置与图片路径。
        Assert.Equal("qwen-vl-max", transcriber.ReceivedSettings?.Model);
        Assert.Equal(@"C:\shots\board.png", transcriber.ReceivedImagePath);
        // 工具行成功收束（中文名 + Ok 终态）。
        AssistantMessageViewModel toolRow = viewModel.Messages.Single(message => message.IsToolCall);
        Assert.Equal("vision_transcribe", toolRow.ToolName);
        Assert.Equal("视觉转写", toolRow.ToolDisplayName);
        Assert.Equal(AiToolCallState.Ok, toolRow.ToolState);
    }

    [Fact]
    public async Task ImageAttachmentWithoutBridgeModelFailsBeforeSending()
    {
        var service = new FakeConfirmationService();
        var client = new CapturingAssistantClient();
        var transcriber = new StubVisionTranscriber((_, _) => Task.FromResult("不应被调用"));
        var viewModel = new AssistantViewModel(
            service,
            assistantClient: client,
            visionTranscriber: transcriber)
        {
            InputText = "看看这张图",
        };
        viewModel.Attachments.Add(new AiTextAttachment(
            "shot.png",
            @"C:\shots\shot.png",
            512,
            string.Empty,
            IsImage: true));

        await viewModel.SendAsync(CancellationToken.None);

        // 未配置视觉桥：请求不发出、转写器不调用，错误行给出指引。
        Assert.Equal(string.Empty, client.LastMessage);
        Assert.Null(transcriber.ReceivedSettings);
        AssistantMessageViewModel error = viewModel.Messages.Single(message => message.IsError);
        Assert.Contains("视觉转写", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TranscriberFailureMarksToolRowErrorAndShowsErrorRow()
    {
        var service = new FakeConfirmationService();
        var visionModel = new AiModelConfiguration(
            "vision",
            "千问视觉",
            new AiProviderSettings { Model = "qwen-vl-max" });
        var catalog = new AiModelCatalog([visionModel], visionModel.Id, visionModel.Settings);
        var client = new CapturingAssistantClient();
        var transcriber = new StubVisionTranscriber(
            (_, _) => Task.FromException<string>(new InvalidOperationException("配额不足")));
        var viewModel = new AssistantViewModel(
            service,
            assistantClient: client,
            modelCatalog: catalog,
            visionTranscriber: transcriber,
            getVisualBridgeModelId: () => visionModel.Id)
        {
            InputText = "看看这张图",
        };
        viewModel.Attachments.Add(new AiTextAttachment(
            "shot.png",
            @"C:\shots\shot.png",
            512,
            string.Empty,
            IsImage: true));

        await viewModel.SendAsync(CancellationToken.None);

        // 工具行 Error 收束，主请求未发出，错误行可见。
        AssistantMessageViewModel toolRow = viewModel.Messages.Single(message => message.IsToolCall);
        Assert.Equal(AiToolCallState.Error, toolRow.ToolState);
        Assert.Equal(string.Empty, client.LastMessage);
        AssistantMessageViewModel error = viewModel.Messages.Single(message => message.IsError);
        Assert.Contains("shot.png", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatHistoryPersistsAcrossViewModelInstances()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var service = new FakeConfirmationService();
            var first = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));
            first.Messages.Add(new AssistantMessageViewModel(true, "帮我安排明天的日程"));

            var second = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));

            Assert.Single(second.Sessions);
            Assert.Single(second.Messages);
            Assert.True(second.Messages[0].IsUser);
            Assert.Equal("帮我安排明天的日程", second.Messages[0].Text);
            Assert.Equal("帮我安排明天的日程", second.CurrentSession.Title);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ChatHistoryDropsAdjacentExactDuplicatesFromOlderSessions()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var service = new FakeConfirmationService();
            var first = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));
            first.Messages.Add(new AssistantMessageViewModel(true, "同一个问题"));
            first.Messages.Add(new AssistantMessageViewModel(true, "同一个问题"));

            var second = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));
            var third = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));

            Assert.Single(second.Messages);
            Assert.Equal("同一个问题", second.Messages[0].Text);
            Assert.Single(third.Messages);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ChatHistoryDropsSeparatedLegacyDuplicateUserMessage()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var service = new FakeConfirmationService();
            var first = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));
            first.Messages.Add(new AssistantMessageViewModel(true, "你的模型是什么"));
            first.Messages.Add(new AssistantMessageViewModel(false, "我是助手"));
            first.Messages.Add(new AssistantMessageViewModel(true, "你的模型是什么"));

            var second = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));

            Assert.Equal(2, second.Messages.Count);
            Assert.True(second.Messages[0].IsUser);
            Assert.False(second.Messages[1].IsUser);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ChatHistoryRetainsTimestampedDuplicateQuestions()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"cccalendar-chat-{Guid.NewGuid():N}.json");
        try
        {
            var service = new FakeConfirmationService();
            var first = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));
            DateTimeOffset submitted = DateTimeOffset.UtcNow;
            first.Messages.Add(new AssistantMessageViewModel(true, "重复问题", submitted));
            first.Messages.Add(new AssistantMessageViewModel(false, "回答", submitted.AddMilliseconds(500)));
            first.Messages.Add(new AssistantMessageViewModel(true, "重复问题", submitted.AddSeconds(10)));

            var second = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));

            Assert.Equal(3, second.Messages.Count);
            Assert.Equal("重复问题", second.Messages[0].Text);
            Assert.Equal("回答", second.Messages[1].Text);
            Assert.Equal("重复问题", second.Messages[2].Text);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ChatHistoryRestoresThinkingText()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"cccalendar-chat-{Guid.NewGuid():N}.json");
        try
        {
            var service = new FakeConfirmationService();
            var first = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));
            first.Messages.Add(new AssistantMessageViewModel(
                false,
                "回答",
                DateTimeOffset.UtcNow,
                "查询了日程"));

            var second = new AssistantViewModel(service, chatHistory: new AiChatHistoryStore(filePath));

            Assert.Single(second.Messages);
            Assert.Equal("查询了日程", second.Messages[0].ThinkingText);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SendingTheSameQuestionTwiceImmediatelyAppendsItOnlyOnce()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: new CapturingAssistantClient())
        {
            InputText = "相同问题",
        };

        // The fake client completes immediately, reproducing a second command raised
        // after the first response has already been appended.
        await viewModel.SendAsync(CancellationToken.None);
        viewModel.InputText = "相同问题";
        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(2, viewModel.Messages.Count);
        Assert.True(viewModel.Messages[0].IsUser);
        Assert.False(viewModel.Messages[1].IsUser);
    }

    [Fact]
    public void NewChatAndSwitchKeepsSessionsSeparate()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var service = new FakeConfirmationService();
            var viewModel = new AssistantViewModel(
                service,
                chatHistory: new AiChatHistoryStore(filePath));
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "第一个会话的问题"));

            // P70-06a：新会话插到列表顶部（左侧会话栏的最新优先形态）。
            viewModel.NewChatCommand.Execute(null);
            Assert.Empty(viewModel.Messages);
            Assert.Equal("新对话", viewModel.CurrentSession.Title);
            Assert.Same(viewModel.Sessions[0], viewModel.CurrentSession);
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "第二个会话的问题"));
            Assert.Equal(2, viewModel.Sessions.Count);

            AssistantChatSession firstSession = viewModel.Sessions[1];
            viewModel.CurrentSession = firstSession;
            Assert.Single(viewModel.Messages);
            Assert.Equal("第一个会话的问题", viewModel.Messages[0].Text);

            viewModel.CurrentSession = viewModel.Sessions[0];
            Assert.Equal("第二个会话的问题", viewModel.Messages[0].Text);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// P70-06a：新对话出现在 Sessions 顶部并成为当前会话
    /// （左侧会话栏以「最新在顶」呈现，与 deepseek-harness 会话列表一致）。
    /// </summary>
    [Fact]
    public void NewChatInsertsSessionAtTopAndSelectsIt()
    {
        // 需要真实存储：消息只有在 SaveChatHistory 里才会同步进会话，
        // 无 chatHistory 的 VM 中「当前会话为空」守卫会拦下新建。
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var viewModel = new AssistantViewModel(
                new FakeConfirmationService(),
                chatHistory: new AiChatHistoryStore(filePath));
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "第一个会话的问题"));

            viewModel.NewChatCommand.Execute(null);

            Assert.Equal(2, viewModel.Sessions.Count);
            Assert.Equal("新对话", viewModel.Sessions[0].Title);
            Assert.Same(viewModel.Sessions[0], viewModel.CurrentSession);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// P70-06a：会话栏条目上的删除按钮按会话删除——删除非当前会话不动当前；
    /// 删除当前会话后相邻会话接替选中；删到空时补一个空「新对话」。
    /// </summary>
    [Fact]
    public void DeleteSessionRemovesTargetAndSelectsNeighbor()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var viewModel = new AssistantViewModel(
                new FakeConfirmationService(),
                chatHistory: new AiChatHistoryStore(filePath));
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "一"));
            viewModel.NewChatCommand.Execute(null);
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "二"));
            viewModel.NewChatCommand.Execute(null);
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "三"));
            // 顶部优先排列：[三(当前), 二, 一]。
            AssistantChatSession current = viewModel.CurrentSession;

            // 删除非当前会话：当前会话保持不变。
            AssistantChatSession middle = viewModel.Sessions[1];
            viewModel.DeleteChatCommand.Execute(middle);
            Assert.DoesNotContain(middle, viewModel.Sessions);
            Assert.Same(current, viewModel.CurrentSession);

            // 删除当前会话：相邻会话（原索引处）接替选中。
            viewModel.DeleteChatCommand.Execute(current);
            Assert.DoesNotContain(current, viewModel.Sessions);
            Assert.Single(viewModel.Sessions);
            Assert.Same(viewModel.Sessions[0], viewModel.CurrentSession);
            Assert.Equal("一", viewModel.CurrentSession.Title);

            // 删到空：补一个空「新对话」，列表不为空。
            viewModel.DeleteChatCommand.Execute(viewModel.CurrentSession);
            Assert.Single(viewModel.Sessions);
            Assert.Equal("新对话", viewModel.CurrentSession.Title);
            Assert.Empty(viewModel.CurrentSession.Messages);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>P70-06a：收起状态可切换并通过回调写回设置（持久化由宿主接线）。</summary>
    [Fact]
    public void SessionsPaneCollapseTogglesAndNotifiesPersistenceCallback()
    {
        var persisted = new List<bool>();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantSessionsCollapsed: true,
            assistantSessionsCollapsedChanged: persisted.Add);
        Assert.True(viewModel.IsSessionsPaneCollapsed);

        viewModel.ToggleSessionsPaneCommand.Execute(null);
        Assert.False(viewModel.IsSessionsPaneCollapsed);
        Assert.Equal([false], persisted);

        viewModel.ToggleSessionsPaneCommand.Execute(null);
        Assert.True(viewModel.IsSessionsPaneCollapsed);
        Assert.Equal([false, true], persisted);
    }

    /// <summary>
    /// P70-06a：会话条目的相对时间取最后活跃时间（最后一条带时间戳的消息，
    /// 没有则回退会话创建时间）；空会话不显示时间。粒度按 harness 习惯
    /// （P72 紧凑徽标，不带"前"字）：刚刚 / N分钟 / N小时 / N天 / 超过 7 天用「M月d日」。
    /// </summary>
    [Fact]
    public void SessionItemsShowRelativeTimeOfLastActivity()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero));
            DateTimeOffset twoHoursAgo = clock.GetUtcNow().AddHours(-2);
            DateTimeOffset threeDaysAgo = clock.GetUtcNow().AddDays(-3);
            DateTimeOffset longAgo = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
            var writer = new AssistantViewModel(
                new FakeConfirmationService(),
                timeProvider: clock,
                chatHistory: new AiChatHistoryStore(filePath));
            writer.Messages.Add(new AssistantMessageViewModel(true, "两小时前的问题", twoHoursAgo));

            // 再补两个历史会话（三小时前 / 很久以前），复用同一存储文件。
            writer.NewChatCommand.Execute(null);
            writer.Messages.Add(new AssistantMessageViewModel(true, "三天前的问题", threeDaysAgo));
            writer.NewChatCommand.Execute(null);
            writer.Messages.Add(new AssistantMessageViewModel(true, "很久以前的问题", longAgo));

            var reader = new AssistantViewModel(
                new FakeConfirmationService(),
                timeProvider: clock,
                chatHistory: new AiChatHistoryStore(filePath));

            Assert.Equal(
                ["很久以前的问题", "三天前的问题", "两小时前的问题"],
                reader.Sessions.Select(session => session.Title));
            Assert.Equal("3月2日", reader.Sessions[0].RelativeTimeText);
            Assert.Equal("3天", reader.Sessions[1].RelativeTimeText);
            Assert.Equal("2小时", reader.Sessions[2].RelativeTimeText);

            // 新建空会话（当前）：无最后活跃时间，条目不显示时间。
            reader.NewChatCommand.Execute(null);
            Assert.Equal(string.Empty, reader.CurrentSession.RelativeTimeText);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// P70-06a：空态标记——没有任何带消息的会话时 HasSessionHistory 为 false
    /// （视图据此显示「暂无历史会话」引导）；出现历史会话后翻转为 true。
    /// </summary>
    [Fact]
    public void HasSessionHistoryTracksSessionsWithMessages()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-chat-sessions-{Guid.NewGuid():N}.json");
        try
        {
            var viewModel = new AssistantViewModel(
                new FakeConfirmationService(),
                chatHistory: new AiChatHistoryStore(filePath));
            Assert.False(viewModel.HasSessionHistory);

            viewModel.Messages.Add(new AssistantMessageViewModel(true, "第一条消息"));
            Assert.True(viewModel.HasSessionHistory);

            viewModel.NewChatCommand.Execute(null);
            Assert.True(viewModel.HasSessionHistory);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task PresentAndConfirmDraftUsesPendingPreviewExactlyOnce()
    {
        var service = new FakeConfirmationService();
        var viewModel = new AssistantViewModel(service);

        viewModel.PresentDraft(AiCreationDraft.Todo("Prepare release", null));

        Assert.True(viewModel.HasPendingPreview);
        Assert.Equal("Prepare release", viewModel.PendingPreview!.Title);

        await viewModel.ConfirmPendingAsync(CancellationToken.None);

        Assert.False(viewModel.HasPendingPreview);
        Assert.Equal(1, service.ConfirmCount);
        Assert.Contains(viewModel.Messages, message => message.Text.Contains("已创建", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfirmingDraftNotifiesExternalDataChangedAfterLocalReload()
    {
        int callbackCount = 0;
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            dataChanged: _ =>
            {
                callbackCount++;
                return Task.CompletedTask;
            });
        viewModel.PresentDraft(AiCreationDraft.Todo("同步测试", null));

        await viewModel.ConfirmPendingAsync(CancellationToken.None);

        Assert.Equal(1, callbackCount);
        Assert.False(viewModel.HasPendingPreview);
    }

    [Fact]
    public async Task RefreshFailureAfterConfirmationDoesNotClaimCreationFailed()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            dataChanged: _ => Task.FromException(new InvalidOperationException("refresh unavailable")));
        viewModel.PresentDraft(AiCreationDraft.Todo("刷新失败测试", null));

        await viewModel.ConfirmPendingAsync(CancellationToken.None);

        Assert.False(viewModel.HasPendingPreview);
        Assert.Contains(viewModel.Messages, message => message.Text == "已创建待办：刷新失败测试");
        Assert.Contains(viewModel.Messages, message => message.Text.StartsWith("待办已创建，但界面刷新失败：", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangeSetRequiresConfirmationAndCanBeUndone()
    {
        var creationService = new FakeConfirmationService();
        var changeService = new FakeChangeConfirmationService();
        var viewModel = new AssistantViewModel(creationService, changeService: changeService);
        var draft = new AiChangeSetDraft(
            "Update tasks",
            [new AiTodoStatusChange(Guid.NewGuid(), CcCalendar.Core.Todos.TodoStatus.InProgress)]);

        viewModel.PresentChangeSet(draft);
        await viewModel.ConfirmPendingChangeAsync(CancellationToken.None);
        await viewModel.UndoLastChangeAsync(CancellationToken.None);

        Assert.Equal(1, changeService.ConfirmCount);
        Assert.Equal(1, changeService.UndoCount);
        Assert.False(viewModel.HasPendingChange);
        Assert.Contains(viewModel.Messages, message => message.Text.Contains("已撤销", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConflictingEventWaitsForChoiceBeforeCreationPreview()
    {
        var creationService = new FakeConfirmationService();
        var planningService = new FakePlanningService();
        var viewModel = new AssistantViewModel(
            creationService,
            planningService: planningService);
        AiCreationDraft draft = AiCreationDraft.TimedEvent(
            "Overlapping meeting",
            new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 17, 10, 30, 0, TimeSpan.FromHours(8)),
            "China Standard Time");

        await viewModel.PresentEventDraftAsync(draft, CancellationToken.None);

        Assert.True(viewModel.HasPendingConflict);
        Assert.False(viewModel.HasPendingPreview);

        await viewModel.ResolveConflictAsync(AiConflictChoice.KeepOverlap, CancellationToken.None);

        Assert.False(viewModel.HasPendingConflict);
        Assert.True(viewModel.HasPendingPreview);
    }

    [Fact]
    public async Task ConflictCheckFailureShowsStatusAndDoesNotCreatePreview()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            planningService: new FakePlanningService(checkFailure: true));
        AiCreationDraft draft = AiCreationDraft.TimedEvent(
            "无法检查的会议",
            new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 17, 10, 30, 0, TimeSpan.FromHours(8)),
            "China Standard Time");

        await viewModel.PresentEventDraftAsync(draft, CancellationToken.None);

        Assert.False(viewModel.HasPendingConflict);
        Assert.False(viewModel.HasPendingPreview);
        Assert.Contains(viewModel.Messages, message => message.Text.StartsWith("检查时间冲突失败：", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfirmationFailureKeepsProposalAndShowsStatus()
    {
        var viewModel = new AssistantViewModel(new FakeConfirmationService(shouldFail: true));
        viewModel.PresentDraft(AiCreationDraft.Todo("保留提案", null));

        await viewModel.ConfirmPendingAsync(CancellationToken.None);

        Assert.True(viewModel.HasPendingPreview);
        Assert.Contains(viewModel.Messages, message => message.Text.StartsWith("确认失败：", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAvailableFailureKeepsConflictAndShowsStatus()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            planningService: new FakePlanningService(findSlotsFailure: true));
        AiCreationDraft draft = AiCreationDraft.TimedEvent(
            "冲突会议",
            new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 8, 17, 10, 30, 0, TimeSpan.FromHours(8)),
            "China Standard Time");
        await viewModel.PresentEventDraftAsync(draft, CancellationToken.None);

        await viewModel.ResolveConflictAsync(AiConflictChoice.FindAvailableTime, CancellationToken.None);

        Assert.True(viewModel.HasPendingConflict);
        Assert.Contains(viewModel.Messages, message => message.Text.StartsWith("查找空闲时间失败：", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DailyAndWeeklyDraftCommandsAppendMarkdownMessages()
    {
        var draftService = new FakeDraftService();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            draftService: draftService,
            timeProvider: new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero)),
            timeZoneId: "UTC");

        await viewModel.BuildDailyPlanAsync(CancellationToken.None);
        await viewModel.BuildWeeklyReportAsync(CancellationToken.None);

        Assert.Equal(2, viewModel.Messages.Count);
        Assert.Contains("# Daily", viewModel.Messages[0].Text, StringComparison.Ordinal);
        Assert.Contains("# Weekly", viewModel.Messages[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAppendsUserAndAssistantMessagesAndClearsInput()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: new FakeAssistantClient())
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(string.Empty, viewModel.InputText);
        Assert.Equal(2, viewModel.Messages.Count);
        Assert.True(viewModel.Messages[0].IsUser);
        Assert.Equal("找到发布待办。", viewModel.Messages[1].Text);
    }

    [Fact]
    public async Task SendIncludesPreviousConversationTurnsInRequest()
    {
        var client = new CapturingAssistantClient();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client);
        viewModel.Messages.Add(new AssistantMessageViewModel(true, "查找今天的会议"));
        viewModel.Messages.Add(new AssistantMessageViewModel(false, "今天有一个评审会"));
        viewModel.InputText = "把它改到下午";

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(
            [
                new AiAssistantTurn(true, "查找今天的会议"),
                new AiAssistantTurn(false, "今天有一个评审会"),
            ],
            client.LastConversation);
    }

    [Fact]
    public async Task SendAllowsAQuestionThatAlreadyAppearsInHistory()
    {
        var client = new CapturingAssistantClient();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client);
        viewModel.Messages.Add(new AssistantMessageViewModel(true, "重复问题"));
        viewModel.InputText = "重复问题";

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(3, viewModel.Messages.Count);
        Assert.Equal("重复问题", client.LastMessage);
    }

    [Fact]
    public async Task StreamingDeltasAppendToOneAssistantMessage()
    {
        var client = new StreamingAssistantClient();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.True(client.StreamWasUsed);
        Assert.Equal(2, viewModel.Messages.Count);
        Assert.Equal("找到发布待办。", viewModel.Messages[1].Text);
    }

    /// <summary>
    /// P70-02：reasoning 流式态必须在收束点退出——首个正文分片到达即结束思考，
    /// 否则「思考中…」的 shimmer 会永远转下去。耗时不足 1 秒按 1 秒计。
    /// </summary>
    [Fact]
    public async Task StreamingReasoningEndsWhenTextDeltaArrives()
    {
        var client = new ReasoningThenTextAssistantClient();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        AssistantMessageViewModel assistant = viewModel.Messages[^1];
        Assert.True(assistant.HasThinking);
        Assert.False(assistant.IsReasoningStreaming);
        Assert.Contains("字", assistant.ThinkingTitle, StringComparison.Ordinal);
        Assert.Contains("1 秒", assistant.ThinkingTitle, StringComparison.Ordinal);
    }

    /// <summary>
    /// P70-05：Running 事件即插入工具行（中文名按目录映射）；流结束后仍未收到
    /// 终态的工具行被 finally 兜底收束为 Error，避免呼吸点永远转。
    /// </summary>
    [Fact]
    public async Task ToolCallRunningInsertsRowAndUnfinishedRowsCollapseOnError()
    {
        var client = new GatedToolCallAssistantClient();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        Task sending = viewModel.SendAsync(CancellationToken.None);

        // fake 发出 Running 后挂起：此刻用户消息 + 工具行已按到达序入列。
        Assert.False(sending.IsCompleted);
        Assert.Equal(2, viewModel.Messages.Count);
        AssistantMessageViewModel toolRow = viewModel.Messages[1];
        Assert.True(toolRow.IsToolCall);
        Assert.Equal("query_todos", toolRow.ToolName);
        Assert.Equal("查询待办", toolRow.ToolDisplayName);
        Assert.Equal("query: release", toolRow.ToolSummary);
        Assert.Equal(AiToolCallState.Running, toolRow.ToolState);
        Assert.Equal("参数 query: release", toolRow.ToolDetail);

        client.Release();
        await sending;

        // 挂起的流没有终态事件（如取消/网络断），finally 收束为 Error。
        Assert.Equal(AiToolCallState.Error, toolRow.ToolState);
        Assert.Contains("已中断", toolRow.ToolDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// P70-05：Ok 终态更新对应的 Running 行并补结果摘要；同轮同名多次调用
    /// 各自成行、按到达序配对（最后一次 Running 匹配下一次终态）。
    /// </summary>
    [Fact]
    public async Task ToolCallOkUpdatesMatchingRunningRowPerCall()
    {
        var client = new ScriptedToolCallAssistantClient(
            new AiToolCallUpdate("propose_timed_event", "title: 例会", AiToolCallState.Running),
            new AiToolCallUpdate("propose_timed_event", "title: 例会", AiToolCallState.Ok, "{\"status\":\"pending\"}"),
            new AiToolCallUpdate("propose_timed_event", "title: 评审", AiToolCallState.Running),
            new AiToolCallUpdate("propose_timed_event", "title: 评审", AiToolCallState.Ok, "{\"status\":\"pending\"}"));
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "安排两个会议",
        };

        await viewModel.SendAsync(CancellationToken.None);

        // 写入工具目录里的名字映射成中文短名。
        Assert.Equal(
            "创建日程提案",
            AssistantMessageViewModel.MapToolDisplayName("propose_timed_event"));
        AssistantMessageViewModel[] toolRows = [..
            viewModel.Messages.Where(message => message.IsToolCall)];
        Assert.Equal(2, toolRows.Length);
        Assert.All(toolRows, row => Assert.Equal(AiToolCallState.Ok, row.ToolState));
        Assert.All(toolRows, row => Assert.Equal("创建日程提案", row.ToolDisplayName));
        Assert.Equal("title: 例会", toolRows[0].ToolSummary);
        Assert.Contains("结果 {\"status\":\"pending\"}", toolRows[0].ToolDetail, StringComparison.Ordinal);
        Assert.Equal("title: 评审", toolRows[1].ToolSummary);
        // 工具行在 assistant 正文消息之前（先工具后回答）。
        Assert.True(viewModel.Messages.IndexOf(toolRows[1]) < viewModel.Messages.Count - 1);
        Assert.False(viewModel.Messages[^1].IsToolCall);
    }

    /// <summary>
    /// P70-05：Error 终态转红且异常消息进展开区；无目录映射的工具回退原始名；
    /// 工具行是过程性信息，不写入聊天历史。
    /// </summary>
    [Fact]
    public async Task ToolCallErrorMarksRowAndUnknownToolKeepsRawName()
    {
        var client = new ScriptedToolCallAssistantClient(
            new AiToolCallUpdate("mystery_tool", "x: 1", AiToolCallState.Running),
            new AiToolCallUpdate("mystery_tool", "x: 1", AiToolCallState.Error, "boom"));
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        AssistantMessageViewModel toolRow = Assert.Single(
            viewModel.Messages,
            message => message.IsToolCall);
        Assert.Equal(AiToolCallState.Error, toolRow.ToolState);
        Assert.Equal("mystery_tool", toolRow.ToolDisplayName);
        Assert.Contains("结果 boom", toolRow.ToolDetail, StringComparison.Ordinal);
        // 不持久化：会话存储里没有工具行（空文本条目）。
        Assert.All(
            viewModel.CurrentSession.Messages,
            stored => Assert.True(stored.Text.Length > 0));
    }

    /// <summary>
    /// P70-03：同一轮内工具子轮的多个 Usage 事件合并挂到同一 assistant 消息，
    /// 且 StepCount 按事件数累计、TurnCount 按完成回合累计。
    /// </summary>
    [Fact]
    public async Task UsageUpdatesMergeIntoOneMessageAndCountSteps()
    {
        var client = new ScriptedUsageAssistantClient(
        [
            new AiUsageUpdate(100, 20, 40, CacheWriteTokens: 7),
            new AiUsageUpdate(50, 10, 5, ReasoningTokens: 3),
        ]);
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        AssistantMessageViewModel assistant = viewModel.Messages[^1];
        Assert.False(assistant.IsUser);
        Assert.Equal(150, assistant.Usage!.PromptTokens);
        Assert.Equal(30, assistant.Usage.CompletionTokens);
        Assert.Equal(45, assistant.Usage.CachedPromptTokens);
        Assert.Equal(7, assistant.Usage.CacheWriteTokens);
        Assert.Equal(3, assistant.Usage.ReasoningTokens);
        Assert.Equal(2, viewModel.StepCount);
        Assert.Equal(1, viewModel.TurnCount);
        Assert.Equal(150, viewModel.TotalPromptTokens);
    }

    /// <summary>P70-03：跨轮累计属性正确，且每轮用量挂在各自的 assistant 消息上不串轮。</summary>
    [Fact]
    public async Task UsageTotalsAccumulateAcrossTurns()
    {
        var client = new ScriptedUsageAssistantClient(
        [
            new AiUsageUpdate(100, 20, 60, CacheWriteTokens: 2),
        ],
        [
            new AiUsageUpdate(50, 10),
        ]);
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);
        viewModel.InputText = "再查一次";
        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(150, viewModel.TotalPromptTokens);
        Assert.Equal(30, viewModel.TotalCompletionTokens);
        Assert.Equal(60, viewModel.TotalCachedPromptTokens);
        Assert.Equal(2, viewModel.TotalCacheWriteTokens);
        Assert.Equal(2, viewModel.TurnCount);
        Assert.Equal(2, viewModel.StepCount);
        Assert.Equal(100, viewModel.Messages[1].Usage!.PromptTokens);
        Assert.Equal(50, viewModel.Messages[3].Usage!.PromptTokens);
    }

    /// <summary>
    /// P70-04：两轮（3 个子轮）后，时钟胶囊摘要为「2 轮 · 3 步」；
    /// 时钟未推进（LLM 未计时）时不拼 tok/s。
    /// </summary>
    [Fact]
    public async Task SessionStatsSummaryCountsTurnsAndSteps()
    {
        var client = new ScriptedUsageAssistantClient(
        [
            new AiUsageUpdate(100, 20, 40, CacheWriteTokens: 7),
            new AiUsageUpdate(50, 10),
        ],
        [
            new AiUsageUpdate(30, 5),
        ]);
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);
        viewModel.InputText = "再查一次";
        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal("2 轮 · 3 步", viewModel.SessionStatsSummary);
    }

    /// <summary>P70-04：Token 胶囊摘要用 k 单位缩写（3245 → 3.2k）；无缓存数据不显示百分比。</summary>
    [Fact]
    public async Task TokenStatsSummaryUsesKUnitsWithoutCachePercent()
    {
        var client = new ScriptedUsageAssistantClient(
        [
            new AiUsageUpdate(1000, 2245),
        ]);
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal("3.2k tokens", viewModel.TokenStatsSummary);
        Assert.Contains(viewModel.TokenStatRows, row => row is { Label: "未缓存输入", Value: "1000" });
        Assert.DoesNotContain(viewModel.TokenStatRows, row => row.Label.Contains("命中率", StringComparison.Ordinal));
    }

    /// <summary>P70-04：有缓存数据时 Token 胶囊摘要拼缓存命中率（缓存读 / 总输入）。</summary>
    [Fact]
    public async Task TokenStatsSummaryShowsCacheHitPercent()
    {
        var client = new ScriptedUsageAssistantClient(
        [
            new AiUsageUpdate(100, 20, 40),
        ]);
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)),
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal("120 tokens · 40% 缓存", viewModel.TokenStatsSummary);
        Assert.Contains(viewModel.TokenStatRows, row => row is { Label: "缓存命中率", Value: "40%" });
    }

    /// <summary>
    /// P70-04：tok/s = 总输出 token / 累计 LLM 毫秒 × 1000；时钟由 fake 客户端推进，
    /// 模拟流式消费耗时 2 秒 → 100 tokens / 2000ms = 50 tok/s。
    /// </summary>
    [Fact]
    public async Task TokensPerSecondUsesAccumulatedLlmDuration()
    {
        var clock = new AdjustableTimeProvider();
        var client = new ClockAdvancingUsageClient(clock, new AiUsageUpdate(200, 100));
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            timeProvider: clock,
            assistantClient: client)
        {
            InputText = "查找发布待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(50, viewModel.TokensPerSecond);
        Assert.Contains("50 tok/s", viewModel.SessionStatsSummary, StringComparison.Ordinal);
        Assert.Contains(
            viewModel.SessionStatRows.Single(row => row.Label == "LLM 耗时").Value,
            "2 秒",
            StringComparison.Ordinal);
    }

    /// <summary>P70-04：HasUsageData 初始为 false，纯文本流（无 usage 事件）仍为 false，首个 usage 后翻转。</summary>
    [Fact]
    public async Task HasUsageDataFlipsAfterFirstUsageUpdate()
    {
        var plainViewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)),
            assistantClient: new StreamingAssistantClient())
        {
            InputText = "查找发布待办",
        };
        Assert.False(plainViewModel.HasUsageData);

        await plainViewModel.SendAsync(CancellationToken.None);
        Assert.False(plainViewModel.HasUsageData);

        var usageViewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)),
            assistantClient: new ScriptedUsageAssistantClient([new AiUsageUpdate(10, 5)]))
        {
            InputText = "查找发布待办",
        };
        Assert.False(usageViewModel.HasUsageData);

        await usageViewModel.SendAsync(CancellationToken.None);
        Assert.True(usageViewModel.HasUsageData);
    }

    [Fact]
    public async Task EmptyAssistantResponseShowsRetryableStatus()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: new EmptyAssistantClient())
        {
            InputText = "没有结果的问题",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Contains(viewModel.Messages, message => message.Text == "模型未返回内容，请重试。");
        Assert.True(viewModel.CanRetry);
        Assert.True(viewModel.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task WriteModeTextClaimWithoutToolProposalShowsNotCreatedStatus()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: new TextOnlyAssistantClient())
        {
            Mode = AiAssistantMode.Write,
            InputText = "新建会议日程",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.False(viewModel.HasPendingPreview);
        Assert.Contains(
            viewModel.Messages,
            message => message.Text.Contains("没有生成可确认的写入提案", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopCommandCancelsInFlightRequestAndLeavesRetryAvailable()
    {
        var client = new CancellableAssistantClient();
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            InputText = "取消这个请求",
        };

        Task send = viewModel.SendAsync(CancellationToken.None);
        await client.Started.Task;
        viewModel.StopCommand.Execute(null);
        await send;

        Assert.Contains(viewModel.Messages, message => message.Text == "请求已取消，可点击重试。");
        Assert.True(viewModel.CanRetry);
        Assert.False(viewModel.IsSending);
    }

    [Fact]
    public async Task WriteModeQueuesModelCreationProposalsForConfirmation()
    {
        var client = new ProposalAssistantClient(
            AiCreationDraft.Todo("First", null),
            AiCreationDraft.Todo("Second", null));
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: client)
        {
            Mode = AiAssistantMode.Write,
            InputText = "创建两个待办",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(AiAssistantMode.Write, client.LastMode);
        Assert.Equal("First", viewModel.PendingPreview!.Title);
        await viewModel.ConfirmPendingAsync(CancellationToken.None);
        Assert.Equal("Second", viewModel.PendingPreview!.Title);
    }

    [Fact]
    public async Task WriteModeBatchesTimedEventProposalsIntoOneConfirmation()
    {
        var service = new BatchConfirmationService();
        var client = new ProposalAssistantClient(
            AiCreationDraft.TimedEvent(
                "每日例会",
                new DateTimeOffset(2026, 8, 24, 10, 10, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 24, 11, 10, 0, TimeSpan.Zero),
                "China Standard Time",
                "项目组二楼会议室",
                "365-5683-5623"),
            AiCreationDraft.TimedEvent(
                "每日例会",
                new DateTimeOffset(2026, 8, 25, 10, 10, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 25, 11, 10, 0, TimeSpan.Zero),
                "China Standard Time",
                "项目组二楼会议室",
                "365-5683-5623"));
        var viewModel = new AssistantViewModel(service, assistantClient: client)
        {
            Mode = AiAssistantMode.Write,
            InputText = "创建周一到周二每日例会",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.True(viewModel.HasPendingPreview);
        Assert.Equal("2项日程", viewModel.PendingPreview!.Title);
        await viewModel.ConfirmPendingAsync(CancellationToken.None);

        Assert.Equal(2, service.ConfirmCount);
        Assert.False(viewModel.HasPendingPreview);
    }

    [Fact]
    public void AssistantMessagesUseMarkdownWhileUserMessagesRemainPlainText()
    {
        var userMessage = new AssistantMessageViewModel(true, "**不要加粗**");
        var assistantMessage = new AssistantMessageViewModel(false, "**需要加粗**");

        Assert.False(userMessage.IsMarkdown);
        Assert.True(assistantMessage.IsMarkdown);
    }

    [Fact]
    public async Task AttachedTextFileIsSentOnceAndShownByFileName()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "## 后续日程\n- 8月20日发布", CancellationToken.None);
        try
        {
            var client = new CapturingAssistantClient();
            var viewModel = new AssistantViewModel(
                new FakeConfirmationService(),
                assistantClient: client);

            await viewModel.AddAttachmentsAsync([path], CancellationToken.None);
            Assert.Single(viewModel.Attachments);
            Assert.True(viewModel.SendCommand.CanExecute(null));

            await viewModel.SendAsync(CancellationToken.None);

            Assert.Contains(Path.GetFileName(path), client.LastMessage, StringComparison.Ordinal);
            Assert.Contains("8月20日发布", client.LastMessage, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(path), viewModel.Messages[0].Text, StringComparison.Ordinal);
            Assert.DoesNotContain("8月20日发布", viewModel.Messages[0].Text, StringComparison.Ordinal);
            Assert.Empty(viewModel.Attachments);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FailedSendKeepsAttachmentsForRetry()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "下周一开会", CancellationToken.None);
        try
        {
            var viewModel = new AssistantViewModel(
                new FakeConfirmationService(),
                assistantClient: new CapturingAssistantClient(shouldFail: true));
            await viewModel.AddAttachmentsAsync([path], CancellationToken.None);

            await viewModel.SendAsync(CancellationToken.None);

            Assert.Single(viewModel.Attachments);
            // P70-07：发送失败以错误行项呈现（标题/消息/异常码），
            // 不再把「请求失败」拼进普通助手消息文本。
            AssistantMessageViewModel errorRow = Assert.Single(viewModel.Messages, message => message.IsError);
            Assert.Equal("发送失败", errorRow.ErrorTitle);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// P70-07 错误行插入：发送失败在消息流轮次尾部插入独立错误行项——
    /// 标题「发送失败」+ secondary 消息（异常 Message）+ 等宽错误码（异常类型名），
    /// Text 保持为空（不参与对话回放）。
    /// </summary>
    [Fact]
    public async Task FailedSendInsertsErrorRowWithCodeAndMessage()
    {
        var viewModel = new AssistantViewModel(
            new FakeConfirmationService(),
            assistantClient: new CapturingAssistantClient(shouldFail: true))
        {
            InputText = "必失败的问题",
        };

        await viewModel.SendAsync(CancellationToken.None);

        Assert.Equal(2, viewModel.Messages.Count);
        AssistantMessageViewModel errorRow = viewModel.Messages[^1];
        Assert.True(errorRow.IsError);
        Assert.Equal("发送失败", errorRow.ErrorTitle);
        Assert.Equal("test failure", errorRow.ErrorMessage);
        Assert.Equal("InvalidOperationException", errorRow.ErrorCode);
        Assert.True(errorRow.HasErrorCode);
        Assert.Equal(string.Empty, errorRow.Text);
    }

    /// <summary>
    /// P70-07：错误行不持久化（与工具行同策略）——发送失败是轮次内的
    /// 过程性信息，历史恢复后无需重现。
    /// </summary>
    [Fact]
    public async Task ErrorRowsAreNotPersistedToChatHistory()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"cccalendar-chat-error-{Guid.NewGuid():N}.json");
        try
        {
            var first = new AssistantViewModel(
                new FakeConfirmationService(),
                assistantClient: new CapturingAssistantClient(shouldFail: true),
                chatHistory: new AiChatHistoryStore(filePath))
            {
                InputText = "必失败的问题",
            };
            await first.SendAsync(CancellationToken.None);
            Assert.Contains(first.Messages, message => message.IsError);

            var second = new AssistantViewModel(
                new FakeConfirmationService(),
                chatHistory: new AiChatHistoryStore(filePath));

            Assert.DoesNotContain(second.Messages, message => message.IsError);
            Assert.Single(second.Messages);
            Assert.True(second.Messages[0].IsUser);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task UnsupportedAttachmentTypeIsRejected()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        await File.WriteAllTextAsync(path, "not executable content", CancellationToken.None);
        try
        {
            var viewModel = new AssistantViewModel(
                new FakeConfirmationService(),
                assistantClient: new CapturingAssistantClient());

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => viewModel.AddAttachmentsAsync([path], CancellationToken.None));

            Assert.Contains("不支持", exception.Message, StringComparison.Ordinal);
            Assert.Empty(viewModel.Attachments);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MoreThanFiveAttachmentsAreRejectedWithoutPartialAddition()
    {
        string[] paths = [.. Enumerable.Range(0, 6)
            .Select(_ => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt"))];
        try
        {
            foreach (string path in paths)
            {
                await File.WriteAllTextAsync(path, "日程", CancellationToken.None);
            }

            var viewModel = new AssistantViewModel(
                new FakeConfirmationService(),
                assistantClient: new CapturingAssistantClient());

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => viewModel.AddAttachmentsAsync(paths, CancellationToken.None));

            Assert.Contains("5", exception.Message, StringComparison.Ordinal);
            Assert.Empty(viewModel.Attachments);
        }
        finally
        {
            foreach (string path in paths)
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FakeConfirmationService(bool shouldFail = false) : IAiCreationConfirmationService
    {
        private AiCreationPreview? preview;

        public int ConfirmCount { get; private set; }

        public AiCreationPreview Preview(AiCreationDraft draft)
        {
            preview = new AiCreationPreview(
                Guid.NewGuid(),
                draft.Kind,
                draft.Title,
                [new AiPreviewField("类型", "待办")]);
            return preview;
        }

        public Task<AiCreationResult> ConfirmAsync(
            Guid proposalId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(preview!.ProposalId, proposalId);
            ConfirmCount++;
            if (shouldFail)
            {
                return Task.FromException<AiCreationResult>(new InvalidOperationException("confirmation unavailable"));
            }

            return Task.FromResult(new AiCreationResult(Guid.NewGuid(), AiCreationKind.Todo));
        }

        public void Cancel(Guid proposalId)
        {
        }
    }

    private sealed class BatchConfirmationService : IAiCreationConfirmationService
    {
        private readonly Dictionary<Guid, AiCreationPreview> previews = [];

        public int ConfirmCount { get; private set; }

        public AiCreationPreview Preview(AiCreationDraft draft)
        {
            var preview = new AiCreationPreview(
                Guid.NewGuid(),
                draft.Kind,
                draft.Title,
                [new AiPreviewField("开始", draft.StartAt?.ToString("O") ?? string.Empty)]);
            previews.Add(preview.ProposalId, preview);
            return preview;
        }

        public Task<AiCreationResult> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken)
        {
            Assert.True(previews.Remove(proposalId));
            ConfirmCount++;
            return Task.FromResult(new AiCreationResult(Guid.NewGuid(), AiCreationKind.Event));
        }

        public void Cancel(Guid proposalId) => previews.Remove(proposalId);
    }

    private sealed class FakeChangeConfirmationService : IAiChangeConfirmationService
    {
        private AiChangePreview? preview;

        public int ConfirmCount { get; private set; }

        public int UndoCount { get; private set; }

        public AiChangePreview Preview(AiChangeSetDraft draft)
        {
            preview = new AiChangePreview(Guid.NewGuid(), draft.Description, ["change"]);
            return preview;
        }

        public Task<AiChangeResult> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken)
        {
            Assert.Equal(preview!.ProposalId, proposalId);
            ConfirmCount++;
            return Task.FromResult(new AiChangeResult(1));
        }

        public void Cancel(Guid proposalId)
        {
        }

        public Task<string?> UndoLastAsync(CancellationToken cancellationToken)
        {
            UndoCount++;
            return Task.FromResult<string?>("Update tasks");
        }
    }

    private sealed class FakePlanningService(
        bool checkFailure = false,
        bool findSlotsFailure = false) : IAiPlanningService
    {
        public Task<AiConflictPrompt> CheckConflictAsync(
            AiCreationDraft candidate,
            CancellationToken cancellationToken)
        {
            if (checkFailure)
            {
                return Task.FromException<AiConflictPrompt>(new InvalidOperationException("planning unavailable"));
            }

            return Task.FromResult(new AiConflictPrompt(
                true,
                1,
                [AiConflictChoice.KeepOverlap, AiConflictChoice.FindAvailableTime, AiConflictChoice.Modify]));
        }

        public Task<IReadOnlyList<AiAvailableSlot>> FindAvailableSlotsAsync(
            DateOnly calendarDate,
            TimeSpan duration,
            string timeZoneId,
            CancellationToken cancellationToken)
        {
            if (findSlotsFailure)
            {
                return Task.FromException<IReadOnlyList<AiAvailableSlot>>(
                    new InvalidOperationException("free-slot service unavailable"));
            }

            return Task.FromResult<IReadOnlyList<AiAvailableSlot>>([]);
        }

        public AiTaskDecompositionPreview PreviewTaskDecomposition(AiTaskDecompositionDraft draft)
        {
            throw new NotSupportedException();
        }

        public Task ConfirmTaskDecompositionAsync(Guid proposalId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void CancelTaskDecomposition(Guid proposalId)
        {
        }
    }

    private sealed class FakeDraftService : IAiDraftService
    {
        public Task<AiTextDraft> BuildProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiTextDraft("Project", "# Project"));
        }

        public Task<AiTextDraft> BuildDailyPlanAsync(
            DateOnly calendarDate,
            string timeZoneId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiTextDraft("Daily", "# Daily"));
        }

        public Task<AiTextDraft> BuildWeeklyReportAsync(
            DateOnly weekStart,
            string timeZoneId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiTextDraft("Weekly", "# Weekly"));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    /// <summary>P70-04：可推进时钟——VM 的 LLM 计时走 TimeProvider，测试用它注入固定耗时。</summary>
    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan delta) => now += delta;
    }

    /// <summary>
    /// P70-04：流式消费期间推进时钟的 fake——VM 在 StreamAsync 起止各读一次时钟，
    /// 这里在发出 usage 前推进 2 秒，模拟一次 2 秒的 LLM 调用。
    /// </summary>
    private sealed class ClockAdvancingUsageClient(AdjustableTimeProvider clock, params AiUsageUpdate[] usages) : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await onUpdate(new AiTextDelta("回答"), cancellationToken);
            clock.Advance(TimeSpan.FromSeconds(2));
            foreach (AiUsageUpdate usage in usages)
            {
                await onUpdate(usage, cancellationToken);
            }
        }
    }

    private sealed class FakeAssistantClient : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("查找发布待办", request.UserMessage);
            return Task.FromResult(new AiAssistantResult("找到发布待办。", []));
        }
    }

    private sealed class StreamingAssistantClient : IAiAssistantClient
    {
        public bool StreamWasUsed { get; private set; }

        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            StreamWasUsed = true;
            await onUpdate(new AiTextDelta("找到"), cancellationToken);
            await onUpdate(new AiTextDelta("发布待办。"), cancellationToken);
        }
    }

    /// <summary>P74-02：先发一次上下文构成事件，再发文本。</summary>
    private sealed class CompositionThenTextClient(AiContextCompositionUpdate composition) : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await onUpdate(composition, cancellationToken);
            await onUpdate(new AiTextDelta("回答"), cancellationToken);
        }
    }

    private sealed class ReasoningThenTextAssistantClient : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await onUpdate(new AiReasoningDelta("先查日程再核对待办"), cancellationToken);
            await onUpdate(new AiTextDelta("找到了。"), cancellationToken);
        }
    }

    /// <summary>
    /// P70-03：按用户回合脚本回放 Usage 事件——每回合先发一段正文，
    /// 再依序发出该回合数组内的各子轮用量。
    /// </summary>
    private sealed class ScriptedUsageAssistantClient(params AiUsageUpdate[][] turns) : IAiAssistantClient
    {
        private int turnIndex;

        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await onUpdate(new AiTextDelta("回答"), cancellationToken);
            foreach (AiUsageUpdate usage in turns[turnIndex++])
            {
                await onUpdate(usage, cancellationToken);
            }
        }
    }

    /// <summary>
    /// P70-05：按脚本回放工具调用事件的 fake——先发全部工具事件再发正文分片，
    /// 与真实工具循环「先工具后回答」的到达序一致。
    /// </summary>
    private sealed class ScriptedToolCallAssistantClient(
        params AiToolCallUpdate[] updates) : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            foreach (AiToolCallUpdate update in updates)
            {
                await onUpdate(update, cancellationToken);
            }

            await onUpdate(new AiTextDelta("回答"), cancellationToken);
        }
    }

    /// <summary>
    /// P70-05：发出 Running 后挂起的 fake——验证工具执行中（无终态事件）的
    /// 中间态，以及流结束时的兜底收束。
    /// </summary>
    private sealed class GatedToolCallAssistantClient : IAiAssistantClient
    {
        private readonly TaskCompletionSource release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("应使用流式接口。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await onUpdate(
                new AiToolCallUpdate("query_todos", "query: release", AiToolCallState.Running),
                cancellationToken);
            await release.Task;
        }

        public void Release() => release.TrySetResult();
    }

    private sealed class CapturingAssistantClient(bool shouldFail = false) : IAiAssistantClient
    {
        public string LastMessage { get; private set; } = string.Empty;

        public IReadOnlyList<AiAssistantTurn> LastConversation { get; private set; } = [];

        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken)
        {
            LastMessage = request.UserMessage;
            LastConversation = request.Conversation;
            return shouldFail
                ? Task.FromException<AiAssistantResult>(new InvalidOperationException("test failure"))
                : Task.FromResult(new AiAssistantResult("已读取附件。", []));
        }
    }

    /// <summary>P74-04：视觉转写 fake——记录收到的设置与路径，按脚本返回或抛错。</summary>
    private sealed class StubVisionTranscriber(
        Func<AiProviderSettings, string, Task<string>> behavior) : IVisionTranscriber
    {
        public AiProviderSettings? ReceivedSettings { get; private set; }

        public string? ReceivedImagePath { get; private set; }

        public Task<string> TranscribeAsync(
            AiProviderSettings settings,
            string imageFilePath,
            CancellationToken cancellationToken)
        {
            ReceivedSettings = settings;
            ReceivedImagePath = imageFilePath;
            return behavior(settings, imageFilePath);
        }
    }

    private sealed class EmptyAssistantClient : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AiAssistantResult(string.Empty, []));
    }

    private sealed class TextOnlyAssistantClient : IAiAssistantClient
    {
        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AiAssistantResult(
                "已生成待确认预览，但没有工具提案。",
                []));
    }

    private sealed class CancellableAssistantClient : IAiAssistantClient
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new AiAssistantResult("不会返回", []);
        }
    }

    private sealed class ProposalAssistantClient(params AiCreationDraft[] drafts) : IAiAssistantClient
    {
        public AiAssistantMode LastMode { get; private set; }

        public Task<AiAssistantResult> SendAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken)
        {
            LastMode = request.Mode;
            return Task.FromResult(new AiAssistantResult(
                "已生成待确认草案。",
                [.. drafts.Select(draft => new AiCreationAssistantProposal(draft))]));
        }
    }
}
