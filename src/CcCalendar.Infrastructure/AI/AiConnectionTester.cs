using System.Net.Http.Headers;
using System.Text.Json;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Infrastructure.AI;

public sealed class AiConnectionTester(HttpClient httpClient) : IAiConnectionTester
{
    public async Task<AiConnectionTestResult> TestAsync(
        AiProviderSettings settings,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AiProviderPrivacyPolicy.EnsureAllowed(settings);
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out Uri? endpoint))
        {
            return new AiConnectionTestResult(false, "端点地址无效");
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            return new AiConnectionTestResult(false, "请填写模型名称");
        }

        if (AiProviderSettings.RequiresApiKey(settings.Provider)
            && string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiConnectionTestResult(false, "请先保存 API Key");
        }

        Uri discoveryEndpoint = settings.Provider switch
        {
            AiProviderKind.OpenAiCompatible => AppendPath(endpoint, "models"),
            AiProviderKind.OpenAiResponses => AppendPath(endpoint, "models"),
            AiProviderKind.Anthropic => AppendPath(endpoint, "models"),
            AiProviderKind.Ollama => AppendPath(endpoint, "api/tags"),
            _ => throw new ArgumentOutOfRangeException(nameof(settings)),
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, discoveryEndpoint);
        if (settings.Provider is AiProviderKind.OpenAiCompatible or AiProviderKind.OpenAiResponses)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        else if (settings.Provider == AiProviderKind.Anthropic)
        {
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new AiConnectionTestResult(
                false,
                $"连接失败：HTTP {(int)response.StatusCode}，{ProviderAiAssistantClient.ExtractApiErrorMessage(errorBody)}");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        bool modelFound = settings.Provider == AiProviderKind.Ollama
            ? ContainsOllamaModel(document.RootElement, settings.Model)
            : ContainsOpenAiModel(document.RootElement, settings.Model);
        return modelFound
            ? new AiConnectionTestResult(true, "连接成功")
            : new AiConnectionTestResult(false, "连接成功，但未找到所选模型");
    }

    private static Uri AppendPath(Uri endpoint, string path)
    {
        string baseUrl = endpoint.AbsoluteUri.TrimEnd('/') + "/";
        return new Uri(new Uri(baseUrl), path);
    }

    private static bool ContainsOpenAiModel(JsonElement root, string model)
    {
        return root.TryGetProperty("data", out JsonElement data)
            && data.ValueKind == JsonValueKind.Array
            && data.EnumerateArray().Any(item =>
                item.TryGetProperty("id", out JsonElement id)
                && string.Equals(id.GetString(), model, StringComparison.Ordinal));
    }

    private static bool ContainsOllamaModel(JsonElement root, string model)
    {
        return root.TryGetProperty("models", out JsonElement models)
            && models.ValueKind == JsonValueKind.Array
            && models.EnumerateArray().Any(item =>
                item.TryGetProperty("name", out JsonElement name)
                && string.Equals(name.GetString(), model, StringComparison.Ordinal));
    }
}
