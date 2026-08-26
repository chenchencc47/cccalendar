using System.Collections.ObjectModel;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.ViewModels;

public sealed class AiSettingsViewModel : ObservableObject
{
    private readonly IAiConnectionTester connectionTester;
    private readonly AiModelCatalog modelCatalog;
    private readonly ISecretStore secretStore;
    private readonly Func<AiProviderSettings, CancellationToken, Task>? legacySettingsChanged;
    private readonly Func<IReadOnlyList<AiModelConfiguration>, string?, CancellationToken, Task>?
        configurationsChanged;
    private string apiKeyInput = string.Empty;
    private string connectionStatus = string.Empty;
    private string displayName;
    private string? editingConfigurationId;
    private string endpoint;
    private bool isApiKeyConfigured;
    private bool localOnlyMode;
    private string model;
    private AiProviderKind provider;

    public AiSettingsViewModel(
        AiProviderSettings settings,
        ISecretStore secretStore,
        IAiConnectionTester connectionTester,
        Func<AiProviderSettings, CancellationToken, Task>? settingsChanged = null)
        : this(
            new AiModelCatalog([], null, settings),
            secretStore,
            connectionTester)
    {
        legacySettingsChanged = settingsChanged ?? ((_, _) => Task.CompletedTask);
    }

    public AiSettingsViewModel(
        AiModelCatalog modelCatalog,
        ISecretStore secretStore,
        IAiConnectionTester connectionTester,
        Func<IReadOnlyList<AiModelConfiguration>, string?, CancellationToken, Task>?
            configurationsChanged = null)
    {
        this.modelCatalog = modelCatalog
            ?? throw new ArgumentNullException(nameof(modelCatalog));
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.connectionTester = connectionTester
            ?? throw new ArgumentNullException(nameof(connectionTester));
        this.configurationsChanged = configurationsChanged;
        AiProviderSettings settings = modelCatalog.CurrentSettings;
        provider = settings.Provider;
        endpoint = settings.Endpoint;
        model = settings.Model;
        displayName = modelCatalog.SelectedModel?.DisplayName ?? settings.Model;
        editingConfigurationId = modelCatalog.SelectedModel?.Id;
        localOnlyMode = settings.LocalOnlyMode;
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(CancellationToken.None));
        TestConnectionCommand = new AsyncRelayCommand(
            () => TestConnectionAsync(CancellationToken.None));
        DeleteConfigurationCommand = new AsyncRelayCommand<AiModelConfiguration>(
            configuration => DeleteConfigurationAsync(configuration, CancellationToken.None));
    }

    public AiProviderKind Provider
    {
        get => provider;
        set
        {
            if (SetProperty(ref provider, value))
            {
                ApplyProviderDefaults(value);
                IsApiKeyConfigured = false;
                OnPropertyChanged(nameof(RequiresApiKey));
            }
        }
    }

    public string Endpoint
    {
        get => endpoint;
        set => SetProperty(ref endpoint, value);
    }

    public string Model
    {
        get => model;
        set => SetProperty(ref model, value);
    }

    public string DisplayName
    {
        get => displayName;
        set => SetProperty(ref displayName, value);
    }

    public ObservableCollection<AiModelConfiguration> ConfiguredModels => modelCatalog.Models;

    public AiModelConfiguration? SelectedConfiguration
    {
        get => modelCatalog.SelectedModel;
        set
        {
            if (value is null || value.Id == editingConfigurationId)
            {
                return;
            }

            modelCatalog.SelectedModel = value;
            LoadConfiguration(value);
            OnPropertyChanged();
        }
    }

    public string ApiKeyInput
    {
        get => apiKeyInput;
        set => SetProperty(ref apiKeyInput, value);
    }

    public bool IsApiKeyConfigured
    {
        get => isApiKeyConfigured;
        private set => SetProperty(ref isApiKeyConfigured, value);
    }

    public bool RequiresApiKey => AiProviderSettings.RequiresApiKey(Provider);

    public bool LocalOnlyMode
    {
        get => localOnlyMode;
        set
        {
            if (!SetProperty(ref localOnlyMode, value) || !value)
            {
                return;
            }

            Provider = AiProviderKind.Ollama;
            Endpoint = "http://localhost:11434";
            if (string.IsNullOrWhiteSpace(Model)
                || Model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
            {
                Model = "qwen3:8b";
            }
        }
    }

    public string ConnectionStatus
    {
        get => connectionStatus;
        private set => SetProperty(ref connectionStatus, value);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand TestConnectionCommand { get; }

    public IAsyncRelayCommand<AiModelConfiguration> DeleteConfigurationCommand { get; }

    public async Task LoadSecretStatusAsync(CancellationToken cancellationToken)
    {
        string? secret = await secretStore.ReadAsync(
            AiProviderSettings.GetCredentialIdentifier(Provider),
            cancellationToken);
        IsApiKeyConfigured = !string.IsNullOrEmpty(secret);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out _))
        {
            ConnectionStatus = "端点地址无效";
            return;
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            ConnectionStatus = "请填写模型名称";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ConnectionStatus = "请填写配置名称";
            return;
        }

        try
        {
            AiProviderPrivacyPolicy.EnsureAllowed(CreateSettings());
        }
        catch (InvalidOperationException exception)
        {
            ConnectionStatus = exception.Message;
            return;
        }

        if (!string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            await secretStore.WriteAsync(
                AiProviderSettings.GetCredentialIdentifier(Provider),
                ApiKeyInput,
                cancellationToken);
            ApiKeyInput = string.Empty;
            IsApiKeyConfigured = true;
        }

        AiProviderSettings settings = CreateSettings();
        if (configurationsChanged is null)
        {
            await legacySettingsChanged!(settings, cancellationToken);
            ConnectionStatus = "设置已保存";
            return;
        }

        var configuration = new AiModelConfiguration(
            editingConfigurationId ?? Guid.NewGuid().ToString("N"),
            DisplayName.Trim(),
            settings);
        editingConfigurationId = configuration.Id;
        modelCatalog.Upsert(configuration);
        await configurationsChanged(
            [.. modelCatalog.Models],
            modelCatalog.SelectedModel?.Id,
            cancellationToken);
        OnPropertyChanged(nameof(SelectedConfiguration));
        ConnectionStatus = $"已保存：{configuration.DisplayName}";
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        string? apiKey = RequiresApiKey
            ? await secretStore.ReadAsync(
                AiProviderSettings.GetCredentialIdentifier(Provider),
                cancellationToken)
            : null;
        AiConnectionTestResult result = await connectionTester.TestAsync(
            CreateSettings(),
            apiKey,
            cancellationToken);
        ConnectionStatus = result.Message;
    }

    private AiProviderSettings CreateSettings()
    {
        return new AiProviderSettings
        {
            Provider = Provider,
            Endpoint = Endpoint.Trim(),
            Model = Model.Trim(),
            LocalOnlyMode = LocalOnlyMode,
        };
    }

    private async Task DeleteConfigurationAsync(
        AiModelConfiguration? configuration,
        CancellationToken cancellationToken)
    {
        if (configuration is null || configurationsChanged is null)
        {
            return;
        }

        modelCatalog.Remove(configuration);
        AiModelConfiguration? selected = modelCatalog.SelectedModel;
        editingConfigurationId = selected?.Id;
        if (selected is not null)
        {
            LoadConfiguration(selected);
        }

        await configurationsChanged(
            [.. modelCatalog.Models],
            selected?.Id,
            cancellationToken);
        OnPropertyChanged(nameof(SelectedConfiguration));
        ConnectionStatus = $"已删除：{configuration.DisplayName}";
    }

    private void LoadConfiguration(AiModelConfiguration configuration)
    {
        editingConfigurationId = configuration.Id;
        displayName = configuration.DisplayName;
        provider = configuration.Settings.Provider;
        endpoint = configuration.Settings.Endpoint;
        model = configuration.Settings.Model;
        localOnlyMode = configuration.Settings.LocalOnlyMode;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Provider));
        OnPropertyChanged(nameof(Endpoint));
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(LocalOnlyMode));
        OnPropertyChanged(nameof(RequiresApiKey));
        _ = LoadSecretStatusAsync(CancellationToken.None);
    }

    private void ApplyProviderDefaults(AiProviderKind value)
    {
        (string defaultEndpoint, string defaultModel) = value switch
        {
            AiProviderKind.OpenAiCompatible or AiProviderKind.OpenAiResponses =>
                ("https://api.openai.com/v1", "gpt-5-mini"),
            AiProviderKind.Anthropic =>
                ("https://api.anthropic.com/v1", "claude-sonnet-4-5"),
            AiProviderKind.Ollama =>
                ("http://localhost:11434", "qwen3:8b"),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
        Endpoint = defaultEndpoint;
        Model = defaultModel;
        if (value != AiProviderKind.Ollama && localOnlyMode)
        {
            SetProperty(ref localOnlyMode, false, nameof(LocalOnlyMode));
        }
    }
}
