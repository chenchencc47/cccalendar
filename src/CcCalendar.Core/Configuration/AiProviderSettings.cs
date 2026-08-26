namespace CcCalendar.Core.Configuration;

public sealed record AiProviderSettings
{
    public const string ApiKeyCredentialIdentifier = "ai.openai.api-key";
    public const string AnthropicApiKeyCredentialIdentifier = "ai.anthropic.api-key";

    public AiProviderKind Provider { get; init; } = AiProviderKind.OpenAiCompatible;

    public string Endpoint { get; init; } = "https://api.openai.com/v1";

    public string Model { get; init; } = "gpt-5-mini";

    public bool LocalOnlyMode { get; init; }

    public static bool RequiresApiKey(AiProviderKind provider) =>
        provider != AiProviderKind.Ollama;

    public static string GetCredentialIdentifier(AiProviderKind provider) =>
        provider == AiProviderKind.Anthropic
            ? AnthropicApiKeyCredentialIdentifier
            : ApiKeyCredentialIdentifier;
}
