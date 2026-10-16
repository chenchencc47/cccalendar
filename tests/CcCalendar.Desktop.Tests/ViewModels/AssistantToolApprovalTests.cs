using CcCalendar.Core.AI;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

/// <summary>
/// P71-02 工具审批的 VM 层测试：脚本化 IAiAssistantClient 复现 Infrastructure 的事件序
/// （PendingApproval 事件 → 审批回调 → Running/Ok 或 Error），验证审批行落库、
/// 四枚裁决命令、会话级放行与"总是允许"持久化回调。
/// </summary>
public sealed class AssistantToolApprovalTests
{
    [Fact]
    public async Task AllowOnceResumesApprovalRowToOk()
    {
        var client = new ScriptedApprovalClient();
        var viewModel = CreateViewModel(client);

        Task sendTask = SendAsync(viewModel);
        await client.ApprovalShown.Task;
        AssistantMessageViewModel row = GetSingleToolRow(viewModel);
        Assert.Equal(AiToolCallState.PendingApproval, row.ToolState);

        viewModel.AllowToolOnceCommand.Execute(row);
        await sendTask;

        // 允许后 Running 原地复用审批行（不追加第二行），终态 Ok。
        Assert.Equal(AiToolCallState.Ok, row.ToolState);
        Assert.Single(GetToolRows(viewModel));
        Assert.Equal([AiToolApprovalDecision.AllowOnce], client.Decisions);
    }

    [Fact]
    public async Task DenyMarksApprovalRowError()
    {
        var client = new ScriptedApprovalClient();
        var viewModel = CreateViewModel(client);

        Task sendTask = SendAsync(viewModel);
        await client.ApprovalShown.Task;
        AssistantMessageViewModel row = GetSingleToolRow(viewModel);

        viewModel.DenyToolCommand.Execute(row);
        await sendTask;

        Assert.Equal(AiToolCallState.Error, row.ToolState);
        Assert.Contains("用户已拒绝", row.ToolDetail, StringComparison.Ordinal);
        Assert.Equal([AiToolApprovalDecision.Deny], client.Decisions);
    }

    [Fact]
    public async Task AllowForSessionSkipsSecondApprovalPrompt()
    {
        var client = new ScriptedApprovalClient(secondApprovalRound: true);
        var viewModel = CreateViewModel(client);

        Task sendTask = SendAsync(viewModel);
        await client.ApprovalShown.Task;
        AssistantMessageViewModel row = GetSingleToolRow(viewModel);

        viewModel.AllowToolForSessionCommand.Execute(row);
        await sendTask;

        // 第一次裁决：会话允许；第二次同名工具：回调直接放行，不再弹审批行。
        Assert.Equal([AiToolApprovalDecision.AllowForSession, AiToolApprovalDecision.AllowOnce], client.Decisions);
        Assert.Single(GetToolRows(viewModel));
    }

    [Fact]
    public async Task AlwaysAllowInvokesPersistenceCallback()
    {
        var client = new ScriptedApprovalClient();
        string? persistedTool = null;
        var viewModel = CreateViewModel(client, alwaysAllowLocalTool: tool => persistedTool = tool);

        Task sendTask = SendAsync(viewModel);
        await client.ApprovalShown.Task;
        AssistantMessageViewModel row = GetSingleToolRow(viewModel);

        viewModel.AlwaysAllowToolCommand.Execute(row);
        await sendTask;

        Assert.Equal("write_file", persistedTool);
        Assert.Equal([AiToolApprovalDecision.AlwaysAllow], client.Decisions);
    }

    [Fact]
    public async Task CancelledStreamClosesPendingApprovalRow()
    {
        var client = new ScriptedApprovalClient(cancelDuringApproval: true);
        var viewModel = CreateViewModel(client);

        await SendAsync(viewModel);

        // 流被取消：finally 兜底把 PendingApproval 行收束为 Error"已中断"。
        AssistantMessageViewModel row = GetSingleToolRow(viewModel);
        Assert.Equal(AiToolCallState.Error, row.ToolState);
        Assert.Contains("已中断", row.ToolDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateApprovalCommandIsIgnored()
    {
        var client = new ScriptedApprovalClient();
        var viewModel = CreateViewModel(client);

        Task sendTask = SendAsync(viewModel);
        await client.ApprovalShown.Task;
        AssistantMessageViewModel row = GetSingleToolRow(viewModel);

        viewModel.AllowToolOnceCommand.Execute(row);
        viewModel.DenyToolCommand.Execute(row); // 连点第二下：幂等忽略
        await sendTask;

        Assert.Equal([AiToolApprovalDecision.AllowOnce], client.Decisions);
        Assert.Equal(AiToolCallState.Ok, row.ToolState);
    }

    private static Task SendAsync(AssistantViewModel viewModel)
    {
        viewModel.InputText = "帮我写文件";
        return viewModel.SendAsync(CancellationToken.None);
    }

    private static List<AssistantMessageViewModel> GetToolRows(AssistantViewModel viewModel)
    {
        return [.. viewModel.Messages.Where(item => item.IsToolCall)];
    }

    private static AssistantMessageViewModel GetSingleToolRow(AssistantViewModel viewModel)
    {
        return Assert.Single(GetToolRows(viewModel));
    }

    private static AssistantViewModel CreateViewModel(
        IAiAssistantClient client,
        Action<string>? alwaysAllowLocalTool = null) =>
        new(
            new StubConfirmationService(),
            assistantClient: client,
            alwaysAllowLocalTool: alwaysAllowLocalTool);

    /// <summary>
    /// 脚本化客户端：按 Infrastructure 的审批协议发事件/调回调。
    /// cancelDuringApproval=true 时在等待裁决前抛取消（模拟用户停止）；
    /// secondApprovalRound=true 时第一次裁决后再发起一次同名审批回调。
    /// </summary>
    private sealed class ScriptedApprovalClient(bool secondApprovalRound = false, bool cancelDuringApproval = false)
        : IAiAssistantClient
    {
        private const string ToolName = "write_file";
        private const string ArgsSummary = "path: out.txt";

        public TaskCompletionSource ApprovalShown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<AiToolApprovalDecision> Decisions { get; } = [];

        public Task<AiAssistantResult> SendAsync(AiAssistantRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException("审批测试只走流式路径。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            await Emit(onUpdate, new AiToolCallUpdate(ToolName, ArgsSummary, AiToolCallState.PendingApproval), cancellationToken);
            ApprovalShown.TrySetResult();
            if (cancelDuringApproval)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new OperationCanceledException(cancellationToken);
            }

            AiToolApprovalDecision decision = await RequestDecisionAsync(request, cancellationToken);
            if (decision == AiToolApprovalDecision.Deny)
            {
                await Emit(onUpdate, new AiToolCallUpdate(ToolName, ArgsSummary, AiToolCallState.Error, "用户已拒绝"), cancellationToken);
            }
            else
            {
                await Emit(onUpdate, new AiToolCallUpdate(ToolName, ArgsSummary, AiToolCallState.Running), cancellationToken);
                await Emit(onUpdate, new AiToolCallUpdate(ToolName, ArgsSummary, AiToolCallState.Ok, "written"), cancellationToken);
            }

            if (secondApprovalRound)
            {
                // 第二次同名工具：会话内已放行时 VM 回调应直接返回，不再等待 UI。
                // RequestDecisionAsync 内部已记录 Decisions，这里不再重复 Add。
                await RequestDecisionAsync(request, cancellationToken);
            }

            await Emit(onUpdate, new AiTextDelta("done"), cancellationToken);
        }

        private static Task Emit(
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            AiAssistantUpdate update,
            CancellationToken cancellationToken) => onUpdate(update, cancellationToken);

        private async Task<AiToolApprovalDecision> RequestDecisionAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken)
        {
            AiToolApprovalDecision decision = await request.ToolApproval!(
                new AiToolApprovalRequest(ToolName),
                cancellationToken);
            Decisions.Add(decision);
            return decision;
        }
    }

    private sealed class StubConfirmationService : IAiCreationConfirmationService
    {
        public AiCreationPreview Preview(AiCreationDraft draft) => new(
            Guid.NewGuid(),
            draft.Kind,
            draft.Title,
            [new AiPreviewField("类型", "待办"), new AiPreviewField("内容", draft.Content ?? "")]);

        public Task<AiCreationResult> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCreationResult(Guid.NewGuid(), AiCreationKind.Todo));

        public void Cancel(Guid proposalId)
        {
        }
    }
}
