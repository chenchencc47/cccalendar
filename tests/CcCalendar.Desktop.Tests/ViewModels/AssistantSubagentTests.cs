using CcCalendar.Core.AI;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

/// <summary>
/// P71-03 子代理折叠卡的 VM 层测试：脚本化 IAiAssistantClient 复现 Infrastructure
/// ExecuteSubagentAsync 的包装事件序（AiSubagentUpdate 逐个包装转发），验证落卡、
/// 嵌套 lineage、卡内审批行递归查找与取消兜底收束。
/// </summary>
public sealed class AssistantSubagentTests
{
    [Fact]
    public async Task SubagentEventsCreateCardAndMergeUsage()
    {
        var client = new ScriptedSubagentClient();
        var viewModel = CreateViewModel(client);

        await SendAsync(viewModel);

        // 主消息流：1 张子代理卡 + 1 条父级正文消息，无散落的卡内事件。
        AssistantMessageViewModel card = Assert.Single(
            viewModel.Messages, item => item.IsSubagent);
        Assert.Equal(AiToolCallState.Ok, card.SubagentState);
        Assert.Equal("统计待办数量", card.SubagentDescription);
        Assert.True(card.HasUsage);
        Assert.Equal("∑ 150 tokens", card.UsageSummary);

        // 卡内：正文消息（思考结束 + 文本）+ 工具行 Running→Ok。
        AssistantMessageViewModel subAnswer = Assert.Single(
            card.SubagentMessages, item => !item.IsToolCall && !item.IsSubagent);
        Assert.Equal("子代理答复：共 2 条待办", subAnswer.Text);
        AssistantMessageViewModel toolRow = Assert.Single(
            card.SubagentMessages, item => item.IsToolCall);
        Assert.Equal(AiToolCallState.Ok, toolRow.ToolState);

        // usage 双记账：卡头小计 100+50，会话总量并入同值。
        Assert.Equal(100, viewModel.TotalPromptTokens);
        Assert.Equal(50, viewModel.TotalCompletionTokens);

        // 父级最终答复落主消息流（不在卡内）。
        Assert.Contains(viewModel.Messages, item => !item.IsSubagent && item.Text == "最终答复");
    }

    [Fact]
    public async Task NestedSubagentUpdatesRenderLineageTree()
    {
        var client = new ScriptedSubagentClient(SubagentScript.Nested);
        var viewModel = CreateViewModel(client);

        await SendAsync(viewModel);

        // 主消息流只有外层卡 A；内层卡 B 落在 A.SubagentMessages（lineage 树）。
        AssistantMessageViewModel cardA = Assert.Single(
            viewModel.Messages, item => item.IsSubagent);
        Assert.Equal("外层任务", cardA.SubagentDescription);
        Assert.Equal(AiToolCallState.Ok, cardA.SubagentState);
        AssistantMessageViewModel cardB = Assert.Single(
            cardA.SubagentMessages, item => item.IsSubagent);
        Assert.Equal("内层任务", cardB.SubagentDescription);
        Assert.Equal(AiToolCallState.Ok, cardB.SubagentState);

        // B 的正文落在 B 卡内，不外溢到 A 或主消息流。
        AssistantMessageViewModel answerB = Assert.Single(
            cardB.SubagentMessages, item => !item.IsToolCall && !item.IsSubagent);
        Assert.Equal("内层答复", answerB.Text);
        Assert.DoesNotContain(cardA.SubagentMessages, item => item.Text == "内层答复");
        Assert.DoesNotContain(viewModel.Messages, item => item.Text == "内层答复");
    }

    [Fact]
    public async Task ApprovalRowInsideSubagentCardCanBeResolved()
    {
        var client = new ScriptedSubagentClient(SubagentScript.ApprovalInsideSubagent);
        var viewModel = CreateViewModel(client);

        Task sendTask = SendAsync(viewModel);
        await client.ApprovalShown.Task;

        // 审批行在卡内：主消息流没有任何工具行，递归查找能命中卡内行。
        AssistantMessageViewModel card = Assert.Single(
            viewModel.Messages, item => item.IsSubagent);
        AssistantMessageViewModel row = Assert.Single(
            card.SubagentMessages, item => item.IsToolCall);
        Assert.Equal(AiToolCallState.PendingApproval, row.ToolState);
        Assert.DoesNotContain(viewModel.Messages, item => item.IsToolCall);

        viewModel.AllowToolOnceCommand.Execute(row);
        await sendTask;

        Assert.Equal([AiToolApprovalDecision.AllowOnce], client.Decisions);
        Assert.Equal(AiToolCallState.Ok, row.ToolState);
        Assert.Equal(AiToolCallState.Ok, card.SubagentState);
    }

    [Fact]
    public async Task CancelledStreamClosesRunningSubagentCard()
    {
        var client = new ScriptedSubagentClient(SubagentScript.CancelDuringSubagent);
        var viewModel = CreateViewModel(client);

        await SendAsync(viewModel);

        // 流被取消：不发 Completed 终态，finally 兜底把卡收束为失败"已中断"。
        AssistantMessageViewModel card = Assert.Single(
            viewModel.Messages, item => item.IsSubagent);
        Assert.Equal(AiToolCallState.Error, card.SubagentState);
        Assert.Contains("已中断", card.SubagentError, StringComparison.Ordinal);
    }

    private static Task SendAsync(AssistantViewModel viewModel)
    {
        viewModel.InputText = "统计待办数量";
        return viewModel.SendAsync(CancellationToken.None);
    }

    private static AssistantViewModel CreateViewModel(IAiAssistantClient client) =>
        new(new StubConfirmationService(), assistantClient: client);

    private enum SubagentScript
    {
        Standard,
        Nested,
        ApprovalInsideSubagent,
        CancelDuringSubagent,
    }

    /// <summary>
    /// 脚本化客户端：按 Infrastructure ExecuteSubagentAsync 的包装协议发事件——
    /// 子会话事件逐个包成 AiSubagentUpdate(subagentId, description, inner)；
    /// ApprovalInsideSubagent 在卡内弹 write_file 审批并等待裁决；
    /// CancelDuringSubagent 在卡 Running 时抛取消（模拟用户停止）。
    /// </summary>
    private sealed class ScriptedSubagentClient(SubagentScript script = SubagentScript.Standard)
        : IAiAssistantClient
    {
        private const string ToolName = "write_file";
        private const string ToolArgs = "path: out.txt";
        private static readonly Guid InnerSubagentId = Guid.NewGuid();

        public TaskCompletionSource ApprovalShown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<AiToolApprovalDecision> Decisions { get; } = [];

        public Task<AiAssistantResult> SendAsync(AiAssistantRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException("子代理测试只走流式路径。");

        public async Task StreamAsync(
            AiAssistantRequest request,
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            Guid subagentId = Guid.NewGuid();
            const string description = "统计待办数量";
            switch (script)
            {
                case SubagentScript.Standard:
                    await Sub(onUpdate, subagentId, description, AiSubagentStarted.Instance, cancellationToken);
                    await Sub(onUpdate, subagentId, description, new AiReasoningDelta("思考中"), cancellationToken);
                    await Sub(onUpdate, subagentId, description, new AiTextDelta("子代理答复：共 2 条待办"), cancellationToken);
                    await Sub(onUpdate, subagentId, description, new AiToolCallUpdate("query_todos", "查询待办", AiToolCallState.Running), cancellationToken);
                    await Sub(onUpdate, subagentId, description, new AiToolCallUpdate("query_todos", "查询待办", AiToolCallState.Ok, "2 条"), cancellationToken);
                    await Sub(onUpdate, subagentId, description, new AiUsageUpdate(100, 50), cancellationToken);
                    await Sub(onUpdate, subagentId, description, new AiSubagentCompleted(true, null), cancellationToken);
                    await Emit(onUpdate, new AiTextDelta("最终答复"), cancellationToken);
                    break;
                case SubagentScript.Nested:
                    await Sub(onUpdate, subagentId, "外层任务", AiSubagentStarted.Instance, cancellationToken);
                    await Sub(onUpdate, subagentId, "外层任务",
                        new AiSubagentUpdate(InnerSubagentId, "内层任务", AiSubagentStarted.Instance), cancellationToken);
                    await Sub(onUpdate, subagentId, "外层任务",
                        new AiSubagentUpdate(InnerSubagentId, "内层任务", new AiTextDelta("内层答复")), cancellationToken);
                    await Sub(onUpdate, subagentId, "外层任务",
                        new AiSubagentUpdate(InnerSubagentId, "内层任务", new AiSubagentCompleted(true, null)), cancellationToken);
                    await Sub(onUpdate, subagentId, "外层任务", new AiSubagentCompleted(true, null), cancellationToken);
                    break;
                case SubagentScript.ApprovalInsideSubagent:
                    await Sub(onUpdate, subagentId, description, AiSubagentStarted.Instance, cancellationToken);
                    await Sub(onUpdate, subagentId, description, new AiToolCallUpdate(ToolName, ToolArgs, AiToolCallState.PendingApproval), cancellationToken);
                    ApprovalShown.TrySetResult();
                    AiToolApprovalDecision decision = await request.ToolApproval!(
                        new AiToolApprovalRequest(ToolName), cancellationToken);
                    Decisions.Add(decision);
                    if (decision == AiToolApprovalDecision.Deny)
                    {
                        await Sub(onUpdate, subagentId, description, new AiToolCallUpdate(ToolName, ToolArgs, AiToolCallState.Error, "用户已拒绝"), cancellationToken);
                    }
                    else
                    {
                        await Sub(onUpdate, subagentId, description, new AiToolCallUpdate(ToolName, ToolArgs, AiToolCallState.Running), cancellationToken);
                        await Sub(onUpdate, subagentId, description, new AiToolCallUpdate(ToolName, ToolArgs, AiToolCallState.Ok, "written"), cancellationToken);
                    }

                    await Sub(onUpdate, subagentId, description, new AiSubagentCompleted(true, null), cancellationToken);
                    await Emit(onUpdate, new AiTextDelta("最终答复"), cancellationToken);
                    break;
                case SubagentScript.CancelDuringSubagent:
                    await Sub(onUpdate, subagentId, description, AiSubagentStarted.Instance, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException(cancellationToken);
            }
        }

        private static Task Sub(
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            Guid subagentId,
            string description,
            AiAssistantUpdate inner,
            CancellationToken cancellationToken) =>
            Emit(onUpdate, new AiSubagentUpdate(subagentId, description, inner), cancellationToken);

        private static Task Emit(
            Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
            AiAssistantUpdate update,
            CancellationToken cancellationToken) => onUpdate(update, cancellationToken);
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
