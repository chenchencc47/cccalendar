using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class AssistantLocalAgentTests
{
    [Fact]
    public void IsLocalAgentEnabledDefaultsToFalseWithoutGetter()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsLocalAgentEnabled);
    }

    [Fact]
    public void ModeRejectsLocalAgentWhenDisabled()
    {
        var viewModel = CreateViewModel(localAgentEnabled: () => false);
        viewModel.Mode = AiAssistantMode.Write;

        viewModel.Mode = AiAssistantMode.LocalAgent;

        // 未启用的档位程序化赋值被兜底拦截，保持原档位。
        Assert.Equal(AiAssistantMode.Write, viewModel.Mode);
    }

    [Fact]
    public void ModeAcceptsLocalAgentWhenEnabled()
    {
        var viewModel = CreateViewModel(localAgentEnabled: () => true);

        viewModel.Mode = AiAssistantMode.LocalAgent;

        Assert.Equal(AiAssistantMode.LocalAgent, viewModel.Mode);
    }

    [Fact]
    public void RefreshLocalAgentAvailabilityFallsBackToReadOnlyWhenDisabled()
    {
        bool enabled = true;
        var viewModel = CreateViewModel(localAgentEnabled: () => enabled);
        viewModel.Mode = AiAssistantMode.LocalAgent;

        enabled = false;
        viewModel.RefreshLocalAgentAvailability();

        Assert.Equal(AiAssistantMode.ReadOnly, viewModel.Mode);
        Assert.False(viewModel.IsLocalAgentEnabled);
    }

    [Fact]
    public void RefreshLocalAgentAvailabilityKeepsModeWhenStillEnabled()
    {
        var viewModel = CreateViewModel(localAgentEnabled: () => true);
        viewModel.Mode = AiAssistantMode.LocalAgent;

        viewModel.RefreshLocalAgentAvailability();

        Assert.Equal(AiAssistantMode.LocalAgent, viewModel.Mode);
        Assert.True(viewModel.IsLocalAgentEnabled);
    }

    [Fact]
    public void LocalAgentSettingsWriteBackPublishesWholeRecord()
    {
        LocalAgentSettings? captured = null;
        int invocations = 0;
        var viewModel = new LocalAgentSettingsViewModel(
            new LocalAgentSettings(),
            updated =>
            {
                captured = updated;
                invocations++;
            });

        viewModel.Enabled = true;

        Assert.True(captured!.Enabled);
        // 记录相等（值未变化）不触发写回。
        viewModel.Enabled = true;
        Assert.Equal(1, invocations);
        // 默认值保持不变。
        Assert.True(captured.AllowReadFile);
        Assert.False(captured.AllowWriteFile);
    }

    private static AssistantViewModel CreateViewModel(Func<bool>? localAgentEnabled = null) =>
        new(new StubConfirmationService(), localAgentEnabled: localAgentEnabled);

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
