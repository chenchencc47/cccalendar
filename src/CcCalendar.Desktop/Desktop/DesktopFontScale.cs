namespace CcCalendar.Desktop.Desktop;

/// <summary>
/// 桌面组件紧凑字号的派生规则。
///
/// 桌面组件的正文由「字体大小 × 界面缩放」驱动，但月历格里的日期角标、农历与日程条
/// 需要更小的字号才放得下。此前它们是写死的 9/10/11px，**不跟随设置**——用户拖动
/// 「字体大小 / 界面缩放」滑杆时只有正文变化，格子里的文字纹丝不动。
///
/// 这里把"更小"表达成相对倍数，于是所有文字一起缩放，层级比例保持不变。
/// 倍数取自原先固定值与正文默认值的比例（正文默认 14px）：
/// 角标 10/14 ≈ 0.71，农历与日程条 12/14 ≈ 0.86。
/// </summary>
public static class DesktopFontScale
{
    /// <summary>农历、日程条等"略小于正文"的档位倍数。</summary>
    public const double CaptionRatio = 0.86d;

    /// <summary>日期角标等"明显小于正文"的档位倍数。</summary>
    public const double MicroRatio = 0.71d;

    /// <summary>字号下限，避免缩放调小时文字小到不可读。</summary>
    public const double MinimumFontSize = 8d;

    /// <summary>略小于正文的档位（原固定 11/12px）。</summary>
    public static double Caption(double bodyFontSize, double scale)
        => Clamp(bodyFontSize * CaptionRatio * scale);

    /// <summary>明显小于正文的档位（原固定 9/10px）。</summary>
    public static double Micro(double bodyFontSize, double scale)
        => Clamp(bodyFontSize * MicroRatio * scale);

    private static double Clamp(double value)
        => Math.Max(MinimumFontSize, Math.Round(value, 2));
}
