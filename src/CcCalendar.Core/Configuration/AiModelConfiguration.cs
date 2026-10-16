namespace CcCalendar.Core.Configuration;

/// <summary>
/// 模型目录条目（P74-01）：ContextWindowTokens 为人工填写的上下文窗口元数据
/// （tokens），null = 未配置（旧 settings.json 兼容），供助理页上下文计量条
/// 计算占用百分比；不参与 provider 请求。
/// </summary>
public sealed record AiModelConfiguration(
    string Id,
    string DisplayName,
    AiProviderSettings Settings,
    int? ContextWindowTokens = null);
