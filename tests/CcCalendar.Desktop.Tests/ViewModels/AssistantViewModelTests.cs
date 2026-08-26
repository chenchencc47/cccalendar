using System.IO;
using CcCalendar.Core.AI;
using CcCalendar.Desktop.AI;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class AssistantViewModelTests
{
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

            viewModel.NewChatCommand.Execute(null);
            Assert.Empty(viewModel.Messages);
            Assert.Equal("新对话", viewModel.CurrentSession.Title);
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "第二个会话的问题"));
            Assert.Equal(2, viewModel.Sessions.Count);

            AssistantChatSession firstSession = viewModel.Sessions[0];
            viewModel.CurrentSession = firstSession;
            Assert.Single(viewModel.Messages);
            Assert.Equal("第一个会话的问题", viewModel.Messages[0].Text);

            viewModel.CurrentSession = viewModel.Sessions[1];
            Assert.Equal("第二个会话的问题", viewModel.Messages[0].Text);
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
            Assert.Contains(viewModel.Messages, message => message.Text.StartsWith("请求失败", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
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
