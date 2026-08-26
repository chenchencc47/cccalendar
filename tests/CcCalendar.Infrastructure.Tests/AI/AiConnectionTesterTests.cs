using System.Net;
using System.Text;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Infrastructure.AI;

namespace CcCalendar.Infrastructure.Tests.AI;

public sealed class AiConnectionTesterTests
{
    [Fact]
    public async Task TestsAllProvidersUsingTheirNativeDiscoveryEndpoints()
    {
        var handler = new QueueHttpMessageHandler(
            "{\"data\":[{\"id\":\"gpt-5-mini\"}]}",
            "{\"data\":[{\"id\":\"gpt-5-mini\"}]}",
            "{\"data\":[{\"id\":\"claude-sonnet-4-5\"}]}",
            "{\"models\":[{\"name\":\"qwen3:8b\"}]}");
        var tester = new AiConnectionTester(new HttpClient(handler));
        var openAi = new AiProviderSettings
        {
            Provider = AiProviderKind.OpenAiCompatible,
            Endpoint = "https://example.test/v1",
            Model = "gpt-5-mini",
        };
        var ollama = new AiProviderSettings
        {
            Provider = AiProviderKind.Ollama,
            Endpoint = "http://localhost:11434",
            Model = "qwen3:8b",
        };
        var responses = openAi with { Provider = AiProviderKind.OpenAiResponses };
        var anthropic = new AiProviderSettings
        {
            Provider = AiProviderKind.Anthropic,
            Endpoint = "https://api.anthropic.test/v1",
            Model = "claude-sonnet-4-5",
        };

        AiConnectionTestResult openAiResult = await tester.TestAsync(
            openAi,
            "test-secret",
            CancellationToken.None);
        AiConnectionTestResult responsesResult = await tester.TestAsync(
            responses,
            "responses-secret",
            CancellationToken.None);
        AiConnectionTestResult anthropicResult = await tester.TestAsync(
            anthropic,
            "anthropic-secret",
            CancellationToken.None);
        AiConnectionTestResult ollamaResult = await tester.TestAsync(
            ollama,
            null,
            CancellationToken.None);

        Assert.True(openAiResult.Success);
        Assert.True(responsesResult.Success);
        Assert.True(anthropicResult.Success);
        Assert.True(ollamaResult.Success);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization!.Scheme);
        Assert.Equal("test-secret", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.EndsWith("/v1/models", handler.Requests[0].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.Requests[1].Headers.Authorization!.Scheme);
        Assert.EndsWith("/v1/models", handler.Requests[1].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("anthropic-secret", handler.Requests[2].Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", handler.Requests[2].Headers.GetValues("anthropic-version").Single());
        Assert.EndsWith("/v1/models", handler.Requests[2].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Null(handler.Requests[3].Headers.Authorization);
        Assert.EndsWith("/api/tags", handler.Requests[3].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
            });
        }
    }
}
