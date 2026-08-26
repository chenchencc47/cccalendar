namespace CcCalendar.Core.AI;

public enum AiAssistantMode
{
    ReadOnly,
    Write,
}

public sealed record AiAssistantTurn(bool IsUser, string Text);

public sealed record AiAssistantRequest
{
    public AiAssistantRequest(
        string userMessage,
        AiAssistantMode mode,
        IReadOnlyList<AiAssistantTurn>? conversation = null)
    {
        UserMessage = userMessage;
        Mode = mode;
        Conversation = conversation ?? [];
    }

    public string UserMessage { get; }

    public AiAssistantMode Mode { get; }

    public IReadOnlyList<AiAssistantTurn> Conversation { get; }
}

public abstract record AiAssistantProposal;

public sealed record AiCreationAssistantProposal(AiCreationDraft Draft) : AiAssistantProposal;

public sealed record AiChangeAssistantProposal(AiChangeSetDraft Draft) : AiAssistantProposal;

public sealed record AiAssistantResult(
    string Text,
    IReadOnlyList<AiAssistantProposal> Proposals);

public abstract record AiAssistantUpdate;

public sealed record AiTextDelta(string Text) : AiAssistantUpdate;

/// <summary>模型返回的思考内容增量；是否返回由模型和 API 决定，不由 UI 开关控制。</summary>
public sealed record AiReasoningDelta(string Text) : AiAssistantUpdate;

public sealed record AiProposalUpdate(AiAssistantProposal Proposal) : AiAssistantUpdate;
