namespace CcCalendar.Core.Configuration;

/// <summary>
/// AI 助手"本地代理"档的设置（P71-01，序列化为 localAgent 节点）。
/// Enabled 是总开关，默认关（Q6 裁决）；各工具独立白名单开关——只读三件套
/// 默认允许（可关），写入两件套默认关，开启后每次调用仍需运行时审批（P71-02）。
/// </summary>
public sealed record LocalAgentSettings
{
    public bool Enabled { get; init; }

    public bool AllowReadFile { get; init; } = true;

    public bool AllowListDirectory { get; init; } = true;

    public bool AllowSearchFiles { get; init; } = true;

    public bool AllowWriteFile { get; init; }

    public bool AllowRunCommand { get; init; }

    /// <summary>
    /// "总是允许"白名单（P71-02）：用户在审批行选"总是允许"后写入，工具名集合；
    /// 次序保持写入序，设置页可整体清除。命中后不再弹审批。
    /// </summary>
    public IReadOnlyList<string> AlwaysAllowTools { get; init; } = [];
}
