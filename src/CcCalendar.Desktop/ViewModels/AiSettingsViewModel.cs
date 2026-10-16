using System.Collections.ObjectModel;
using System.Globalization;
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
    // P74-04：视觉桥选择的即时持久化回调（宿主写设置），null = 未接线。
    private readonly Action<string?>? visualBridgeChanged;
    private string apiKeyInput = string.Empty;
    private string connectionStatus = string.Empty;
    private string contextWindowTokensText = string.Empty;
    private string displayName;
    private string? editingConfigurationId;
    private string endpoint;
    private bool isApiKeyConfigured;
    private bool localOnlyMode;
    private string model;
    private AiProviderKind provider;
    private string visualBridgeModelId;

    /// <summary>P74-04：视觉桥「不启用」哨兵——Id 为空串，随 ComboBox 选中即关闭。</summary>
    internal const string DisableVisualBridgeModelId = "";

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
            configurationsChanged = null,
        string? visualBridgeModelId = null,
        Action<string?>? visualBridgeChanged = null)
    {
        this.modelCatalog = modelCatalog
            ?? throw new ArgumentNullException(nameof(modelCatalog));
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.connectionTester = connectionTester
            ?? throw new ArgumentNullException(nameof(connectionTester));
        this.configurationsChanged = configurationsChanged;
        this.visualBridgeChanged = visualBridgeChanged;
        this.visualBridgeModelId = visualBridgeModelId ?? DisableVisualBridgeModelId;
        AiProviderSettings settings = modelCatalog.CurrentSettings;
        provider = settings.Provider;
        endpoint = settings.Endpoint;
        model = settings.Model;
        displayName = modelCatalog.SelectedModel?.DisplayName ?? settings.Model;
        editingConfigurationId = modelCatalog.SelectedModel?.Id;
        localOnlyMode = settings.LocalOnlyMode;
        contextWindowTokensText = modelCatalog.SelectedModel?.ContextWindowTokens is { } window
            ? window.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(CancellationToken.None));
        TestConnectionCommand = new AsyncRelayCommand(
            () => TestConnectionAsync(CancellationToken.None));
        DeleteConfigurationCommand = new AsyncRelayCommand<AiModelConfiguration>(
            configuration => DeleteConfigurationAsync(configuration, CancellationToken.None));
        RefreshVisualBridgeOptions();
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

    /// <summary>
    /// 上下文窗口（tokens）表单文本（P74-01）：空 = 未设置；保存时解析为正整数
    /// 写入 AiModelConfiguration.ContextWindowTokens，非法输入阻止保存。
    /// </summary>
    public string ContextWindowTokensText
    {
        get => contextWindowTokensText;
        set => SetProperty(ref contextWindowTokensText, value);
    }

    public ObservableCollection<AiModelConfiguration> ConfiguredModels => modelCatalog.Models;

    /// <summary>
    /// P74-04：视觉转写模型 Id（空串 = 不启用）。选择即生效——与主模型选择
    /// 一致地即时持久化，无需点保存。
    /// </summary>
    public string VisualBridgeModelId
    {
        get => visualBridgeModelId;
        set
        {
            string normalized = value ?? DisableVisualBridgeModelId;
            if (SetProperty(ref visualBridgeModelId, normalized))
            {
                visualBridgeChanged?.Invoke(
                    normalized.Length == 0 ? null : normalized);
            }
        }
    }

    /// <summary>
    /// P74-04：视觉桥下拉选项——哨兵「不启用」打头，接全部已配置模型
    /// （模型增删后由 RefreshVisualBridgeOptions 重建）。
    /// </summary>
    public IReadOnlyList<AiModelConfiguration> VisualBridgeOptions { get; private set; } = [];

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

        // P74-01：上下文窗口为可选元数据，填了就必须是正整数。
        int? contextWindowTokens = null;
        string windowText = ContextWindowTokensText.Trim();
        if (windowText.Length > 0)
        {
            if (!int.TryParse(windowText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int window)
                || window <= 0)
            {
                ConnectionStatus = "上下文窗口必须是正整数（tokens），留空表示未设置";
                return;
            }

            contextWindowTokens = window;
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
            settings,
            contextWindowTokens);
        editingConfigurationId = configuration.Id;
        modelCatalog.Upsert(configuration);
        RefreshVisualBridgeOptions();
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
        RefreshVisualBridgeOptions();
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
        contextWindowTokensText = configuration.ContextWindowTokens is { } window
            ? window.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Provider));
        OnPropertyChanged(nameof(Endpoint));
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(LocalOnlyMode));
        OnPropertyChanged(nameof(RequiresApiKey));
        OnPropertyChanged(nameof(ContextWindowTokensText));
        _ = LoadSecretStatusAsync(CancellationToken.None);
    }

    /// <summary>
    /// P74-04：重建视觉桥选项集；当前选择的模型被删除时回退「不启用」
    /// 并即时持久化，避免悬挂的 Id 指向不存在的配置。
    /// </summary>
    private void RefreshVisualBridgeOptions()
    {
        VisualBridgeOptions =
        [
            new AiModelConfiguration(
                DisableVisualBridgeModelId,
                "不启用",
                modelCatalog.CurrentSettings),
            .. modelCatalog.Models,
        ];
        OnPropertyChanged(nameof(VisualBridgeOptions));
        if (visualBridgeModelId.Length > 0
            && modelCatalog.Models.All(model => model.Id != visualBridgeModelId))
        {
            VisualBridgeModelId = DisableVisualBridgeModelId;
        }
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
