using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Security;
using CcCalendar.Infrastructure.AI;

namespace CcCalendar.Infrastructure.Tests.AI;

/// <summary>
/// P74-04 视觉桥：ProviderVisionTranscriber 走 OpenAI 兼容 chat completions，
/// 把图片以 data URI 附给视觉模型，取回结构化转写文本。
/// </summary>
public sealed class ProviderVisionTranscriberTests
{
    [Fact]
    public async Task TranscribesImageThroughOpenAiCompatibleChatEndpoint()
    {
        string imagePath = await CreateTemporaryImageAsync(".png", [0x89, 0x50, 0x4E, 0x47]);
        try
        {
            var handler = new QueueHttpMessageHandler(
                "{\"choices\":[{\"message\":{\"content\":\"白板照片，上面列了三条待办\"}}]}");
            var secretStore = new MemorySecretStore();
            await secretStore.WriteAsync(
                AiProviderSettings.ApiKeyCredentialIdentifier,
                "vision-secret",
                CancellationToken.None);
            var transcriber = new ProviderVisionTranscriber(new HttpClient(handler), secretStore);
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://dashscope.example/v1",
                Model = "qwen-vl-max",
            };

            string transcript = await transcriber.TranscribeAsync(
                settings,
                imagePath,
                CancellationToken.None);

            Assert.Equal("白板照片，上面列了三条待办", transcript);
            HttpRequestMessage request = handler.Requests.Single();
            Assert.EndsWith(
                "/v1/chat/completions",
                request.RequestUri!.AbsoluteUri,
                StringComparison.Ordinal);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("vision-secret", request.Headers.Authorization.Parameter);
            string body = handler.ReadRequestBody(0);
            JsonNode root = JsonNode.Parse(body)!;
            Assert.Equal("qwen-vl-max", (string?)root["model"]);
            JsonArray messages = root["messages"]!.AsArray();
            Assert.Equal(2, messages.Count);
            Assert.Equal("system", (string?)messages[0]!["role"]);
            Assert.Equal("user", (string?)messages[1]!["role"]);
            JsonArray content = messages[1]!["content"]!.AsArray();
            JsonObject imagePart = content
                .OfType<JsonObject>()
                .Single(part => (string?)part["type"] == "image_url");
            string? dataUrl = (string?)imagePart["image_url"]!["url"];
            Assert.StartsWith("data:image/png;base64,", dataUrl, StringComparison.Ordinal);
            // 文本部分给出转写任务指令。
            JsonObject textPart = content
                .OfType<JsonObject>()
                .Single(part => (string?)part["type"] == "text");
            Assert.Contains("转写", (string?)textPart["text"], StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task RejectsNonOpenAiCompatibleProviders()
    {
        string imagePath = await CreateTemporaryImageAsync(".png", [0x89, 0x50]);
        try
        {
            var transcriber = new ProviderVisionTranscriber(
                new HttpClient(new QueueHttpMessageHandler()),
                new MemorySecretStore());
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.Anthropic,
                Endpoint = "https://api.anthropic.test/v1",
                Model = "claude-sonnet-4-5",
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => transcriber.TranscribeAsync(settings, imagePath, CancellationToken.None));

            Assert.Contains("OpenAI 兼容", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task PropagatesApiErrorWithStatusAndMessage()
    {
        string imagePath = await CreateTemporaryImageAsync(".jpg", [0xFF, 0xD8]);
        try
        {
            var handler = new QueueHttpMessageHandler(
                "{\"error\":{\"message\":\"Invalid image format\"}}")
            {
                ResponseStatus = HttpStatusCode.BadRequest,
            };
            var secretStore = new MemorySecretStore();
            await secretStore.WriteAsync(
                AiProviderSettings.ApiKeyCredentialIdentifier,
                "vision-secret",
                CancellationToken.None);
            var transcriber = new ProviderVisionTranscriber(new HttpClient(handler), secretStore);
            var settings = new AiProviderSettings
            {
                Provider = AiProviderKind.OpenAiCompatible,
                Endpoint = "https://dashscope.example/v1",
                Model = "qwen-vl-max",
            };

            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => transcriber.TranscribeAsync(settings, imagePath, CancellationToken.None));

            Assert.Contains("400", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Invalid image format", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static async Task<string> CreateTemporaryImageAsync(string extension, byte[] bytes)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-vision-{Guid.NewGuid():N}{extension}");
        await File.WriteAllBytesAsync(path, bytes, CancellationToken.None);
        return path;
    }

    private sealed class MemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> values = [];

        public Task<string?> ReadAsync(string identifier, CancellationToken cancellationToken)
        {
            values.TryGetValue(identifier, out string? value);
            return Task.FromResult(value);
        }

        public Task WriteAsync(string identifier, string secret, CancellationToken cancellationToken)
        {
            values[identifier] = secret;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string identifier, CancellationToken cancellationToken)
        {
            values.Remove(identifier);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 记录请求并按序回放固定响应体的 handler（请求体读出后仍可断言——
    /// HttpClient 发送完成后 Content 已被缓冲）。
    /// </summary>
    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);
        private readonly List<string> recordedBodies = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            string requestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            recordedBodies.Add(requestBody);
            return new HttpResponseMessage(ResponseStatus)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
            };
        }

        public string ReadRequestBody(int index) => recordedBodies[index];
    }
}
