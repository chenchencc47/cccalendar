using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Security;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class AiSettingsViewModelTests
{
    [Fact]
    public async Task SaveAddsNamedConfigurationToSharedCatalog()
    {
        var catalog = new AiModelCatalog([], null, new AiProviderSettings());
        IReadOnlyList<AiModelConfiguration>? savedModels = null;
        string? savedSelection = null;
        var viewModel = new AiSettingsViewModel(
            catalog,
            new MemorySecretStore(),
            new RecordingConnectionTester(),
            (models, selectedId, _) =>
            {
                savedModels = models;
                savedSelection = selectedId;
                return Task.CompletedTask;
            })
        {
            DisplayName = "工作 OpenAI",
            Endpoint = "https://example.test/v1",
            Model = "gpt-work",
        };

        await viewModel.SaveAsync(CancellationToken.None);

        AiModelConfiguration configured = Assert.Single(viewModel.ConfiguredModels);
        Assert.Equal("工作 OpenAI", configured.DisplayName);
        Assert.Equal("gpt-work", configured.Settings.Model);
        Assert.Equal(configured.Id, catalog.SelectedModel!.Id);
        Assert.Equal(configured.Id, savedSelection);
        Assert.Equal(savedModels, viewModel.ConfiguredModels);
    }

    [Fact]
    public void SelectingConfiguredModelChangesProviderUsedByConversation()
    {
        var first = new AiModelConfiguration(
            "first",
            "OpenAI",
            new AiProviderSettings { Model = "gpt-first" });
        var second = new AiModelConfiguration(
            "second",
            "Anthropic",
            new AiProviderSettings
            {
                Provider = AiProviderKind.Anthropic,
                Model = "claude-second",
            });
        string? selectedId = null;
        var catalog = new AiModelCatalog(
            [first, second],
            first.Id,
            first.Settings,
            id => selectedId = id);

        catalog.SelectedModel = second;

        Assert.Equal("second", selectedId);
        Assert.Equal("claude-second", catalog.CurrentSettings.Model);
    }

    [Fact]
    public void ReplacingEqualConfigurationKeepsSelectionInsideCatalog()
    {
        var original = new AiModelConfiguration(
            "first",
            "OpenAI",
            new AiProviderSettings { Model = "gpt-first" });
        var catalog = new AiModelCatalog([original], original.Id, original.Settings);
        var replacement = original with { };

        catalog.Upsert(replacement);

        Assert.Same(catalog.Models[0], catalog.SelectedModel);
    }

    [Fact]
    public async Task DeleteRemovesConfigurationAndPersistsRemainingSelection()
    {
        var first = new AiModelConfiguration(
            "first",
            "First",
            new AiProviderSettings { Model = "gpt-first" });
        var second = new AiModelConfiguration(
            "second",
            "Second",
            new AiProviderSettings { Model = "gpt-second" });
        IReadOnlyList<AiModelConfiguration>? savedModels = null;
        string? savedSelection = null;
        var catalog = new AiModelCatalog([first, second], second.Id, first.Settings);
        var viewModel = new AiSettingsViewModel(
            catalog,
            new MemorySecretStore(),
            new RecordingConnectionTester(),
            (models, selectedId, _) =>
            {
                savedModels = models;
                savedSelection = selectedId;
                return Task.CompletedTask;
            });

        await viewModel.DeleteConfigurationCommand.ExecuteAsync(second);

        Assert.Equal(first.Id, Assert.Single(savedModels!).Id);
        Assert.Equal(first.Id, savedSelection);
        Assert.Equal(first.Id, catalog.SelectedModel!.Id);
    }

    [Fact]
    public void ProviderSelectionAppliesUsableProtocolDefaults()
    {
        var viewModel = new AiSettingsViewModel(
            new AiProviderSettings(),
            new MemorySecretStore(),
            new RecordingConnectionTester());

        viewModel.Provider = AiProviderKind.Anthropic;

        Assert.Equal("https://api.anthropic.com/v1", viewModel.Endpoint);
        Assert.Equal("claude-sonnet-4-5", viewModel.Model);
        Assert.True(viewModel.RequiresApiKey);

        viewModel.Provider = AiProviderKind.OpenAiResponses;

        Assert.Equal("https://api.openai.com/v1", viewModel.Endpoint);
        Assert.Equal("gpt-5-mini", viewModel.Model);
        Assert.True(viewModel.RequiresApiKey);
    }

    [Fact]
    public void LocalOnlyModeSwitchesToLoopbackOllamaWithoutApiKey()
    {
        var viewModel = new AiSettingsViewModel(
            new AiProviderSettings(),
            new MemorySecretStore(),
            new RecordingConnectionTester());

        viewModel.LocalOnlyMode = true;

        Assert.Equal(AiProviderKind.Ollama, viewModel.Provider);
        Assert.Equal("http://localhost:11434", viewModel.Endpoint);
        Assert.False(viewModel.RequiresApiKey);
    }

    [Fact]
    public async Task SaveStoresApiKeyOnlyInSecretStoreAndConnectionTestReadsItBack()
    {
        var secretStore = new MemorySecretStore();
        var tester = new RecordingConnectionTester();
        AiProviderSettings? savedSettings = null;
        var viewModel = new AiSettingsViewModel(
            new AiProviderSettings(),
            secretStore,
            tester,
            (updated, _) =>
            {
                savedSettings = updated;
                return Task.CompletedTask;
            })
        {
            Endpoint = "https://example.test/v1",
            Model = "gpt-5-mini",
            ApiKeyInput = "sk-test-only",
        };

        await viewModel.SaveAsync(CancellationToken.None);
        await viewModel.TestConnectionAsync(CancellationToken.None);

        Assert.Equal("sk-test-only", await secretStore.ReadAsync(
            AiProviderSettings.ApiKeyCredentialIdentifier,
            CancellationToken.None));
        Assert.Equal(string.Empty, viewModel.ApiKeyInput);
        Assert.True(viewModel.IsApiKeyConfigured);
        Assert.DoesNotContain("sk-test-only", System.Text.Json.JsonSerializer.Serialize(savedSettings), StringComparison.Ordinal);
        Assert.Equal("sk-test-only", tester.LastApiKey);
        Assert.Equal("连接成功", viewModel.ConnectionStatus);
    }

    [Fact]
    public async Task SaveWaitsForSettingsPersistenceBeforeReportingSuccess()
    {
        var persistenceCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new AiSettingsViewModel(
            new AiProviderSettings(),
            new MemorySecretStore(),
            new RecordingConnectionTester(),
            async (updated, cancellationToken) =>
            {
                Assert.Equal("https://example.test/v1", updated.Endpoint);
                await persistenceCompleted.Task.WaitAsync(cancellationToken);
            })
        {
            Endpoint = "https://example.test/v1",
            Model = "saved-model",
        };

        Task saveTask = viewModel.SaveAsync(CancellationToken.None);

        Assert.False(saveTask.IsCompleted);
        Assert.NotEqual("设置已保存", viewModel.ConnectionStatus);
        persistenceCompleted.SetResult();
        await saveTask;
        Assert.Equal("设置已保存", viewModel.ConnectionStatus);
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

    private sealed class RecordingConnectionTester : IAiConnectionTester
    {
        public string? LastApiKey { get; private set; }

        public Task<AiConnectionTestResult> TestAsync(
            AiProviderSettings settings,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            LastApiKey = apiKey;
            return Task.FromResult(new AiConnectionTestResult(true, "连接成功"));
        }
    }
}
