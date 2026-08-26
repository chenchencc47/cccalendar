using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.AI;

public static class AiProviderPrivacyPolicy
{
    public static void EnsureAllowed(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.LocalOnlyMode)
        {
            return;
        }

        if (settings.Provider != AiProviderKind.Ollama
            || !Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out Uri? endpoint)
            || !endpoint.IsLoopback)
        {
            throw new InvalidOperationException(
                "仅本地模式只允许连接本机回环地址上的 Ollama。");
        }
    }
}

public interface IAiAssistantClient
{
    Task<AiAssistantResult> SendAsync(
        AiAssistantRequest request,
        CancellationToken cancellationToken);

    async Task StreamAsync(
        AiAssistantRequest request,
        Func<AiAssistantUpdate, CancellationToken, Task> onUpdate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        AiAssistantResult result = await SendAsync(request, cancellationToken);
        if (!string.IsNullOrEmpty(result.Text))
        {
            await onUpdate(new AiTextDelta(result.Text), cancellationToken);
        }

        foreach (AiAssistantProposal proposal in result.Proposals)
        {
            await onUpdate(new AiProposalUpdate(proposal), cancellationToken);
        }
    }
}
