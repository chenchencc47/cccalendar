using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.Tests.AI;

public sealed class AiProviderPrivacyPolicyTests
{
    [Fact]
    public void LocalOnlyModeAllowsLoopbackOllamaAndRejectsRemoteEndpoints()
    {
        var local = new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            Endpoint = "http://localhost:11434",
            Model = "qwen3:8b",
            LocalOnlyMode = true,
        };
        var remote = local with { Endpoint = "https://ollama.example.test" };
        var online = local with
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://api.openai.com/v1",
        };

        AiProviderPrivacyPolicy.EnsureAllowed(local);
        Assert.Throws<InvalidOperationException>(() => AiProviderPrivacyPolicy.EnsureAllowed(remote));
        Assert.Throws<InvalidOperationException>(() => AiProviderPrivacyPolicy.EnsureAllowed(online));
    }
}
