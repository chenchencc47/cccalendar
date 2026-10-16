using CcCalendar.Core.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.ViewModels;

/// <summary>
/// 设置页"AI 模型 → 本地代理"区的视图模型（P71-01）：总开关 + 每工具独立白名单
/// 开关，全部即时写回设置（与桌面外观类开关一致，无独立保存按钮）。
/// P71-02 增加审批"总是允许"名单的展示与整体清除。
/// </summary>
public sealed class LocalAgentSettingsViewModel : ObservableObject
{
    private readonly Action<LocalAgentSettings> changed;
    private LocalAgentSettings settings;

    public LocalAgentSettingsViewModel(
        LocalAgentSettings initialSettings,
        Action<LocalAgentSettings>? changed = null)
    {
        settings = initialSettings;
        this.changed = changed ?? (_ => { });
        ClearAlwaysAllowToolsCommand = new RelayCommand(ClearAlwaysAllowTools);
    }

    public bool Enabled
    {
        get => settings.Enabled;
        set => Update(settings with { Enabled = value });
    }

    public bool AllowReadFile
    {
        get => settings.AllowReadFile;
        set => Update(settings with { AllowReadFile = value });
    }

    public bool AllowListDirectory
    {
        get => settings.AllowListDirectory;
        set => Update(settings with { AllowListDirectory = value });
    }

    public bool AllowSearchFiles
    {
        get => settings.AllowSearchFiles;
        set => Update(settings with { AllowSearchFiles = value });
    }

    public bool AllowWriteFile
    {
        get => settings.AllowWriteFile;
        set => Update(settings with { AllowWriteFile = value });
    }

    public bool AllowRunCommand
    {
        get => settings.AllowRunCommand;
        set => Update(settings with { AllowRunCommand = value });
    }

    /// <summary>"总是允许"白名单（P71-02）：审批行选"总是允许"后累积的工具名。</summary>
    public IReadOnlyList<string> AlwaysAllowTools => settings.AlwaysAllowTools;

    /// <summary>白名单非空（P71-02）：控制设置页清除行的可见性。</summary>
    public bool HasAlwaysAllowTools => settings.AlwaysAllowTools.Count > 0;

    /// <summary>白名单展示文本（P71-02）：工具名顿号拼接，空时占位提示。</summary>
    public string AlwaysAllowToolsSummary => settings.AlwaysAllowTools.Count == 0
        ? "暂无（在助理页审批行选择“总是允许”后出现）"
        : string.Join("、", settings.AlwaysAllowTools);

    /// <summary>整体清除"总是允许"白名单（P71-02）：清除后恢复逐次审批。</summary>
    public IRelayCommand ClearAlwaysAllowToolsCommand { get; }

    /// <summary>
    /// 外部（助理页审批"总是允许"）更新设置后的快照刷新（P71-02）：
    /// 只换快照重发通知，不走 changed 写回（调用方已保存）。
    /// </summary>
    public void Refresh(LocalAgentSettings updated)
    {
        settings = updated;
        OnPropertyChanged(string.Empty);
    }

    private void ClearAlwaysAllowTools()
    {
        Update(settings with { AlwaysAllowTools = [] });
    }

    private void Update(LocalAgentSettings updated)
    {
        // 记录相等（值未变化）不触发写回，避免 UI 初始化绑定造成多余持久化。
        if (updated == settings)
        {
            return;
        }

        settings = updated;
        OnPropertyChanged();
        changed(settings);
    }
}
