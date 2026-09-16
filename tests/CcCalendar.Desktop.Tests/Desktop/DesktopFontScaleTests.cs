using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

/// <summary>
/// P64 桌面组件紧凑字号派生规则。
///
/// 修复前：月历格里的日期角标、农历与日程条是固定 9/10/11px，**不跟随**
/// 桌面组件的「字体大小 / 界面缩放」设置——只有正文会变。这组测试锁住派生规则，
/// 确保所有文字一起缩放且层级比例不被破坏。
/// </summary>
public sealed class DesktopFontScaleTests
{
    private const double DefaultBodyFontSize = 14d;

    /// <summary>默认设置下应还原成原先的固定值量级（角标 ≈10、农历 ≈12）。</summary>
    [Fact]
    public void DefaultSettingsReproduceTheOriginalFixedSizes()
    {
        Assert.Equal(9.94d, DesktopFontScale.Micro(DefaultBodyFontSize, 1d), 2);
        Assert.Equal(12.04d, DesktopFontScale.Caption(DefaultBodyFontSize, 1d), 2);
    }

    /// <summary>微档必须小于正文档，农历档必须小于正文档且大于微档。</summary>
    [Fact]
    public void SmallerTiersStayOrderedBelowBodyText()
    {
        double body = DefaultBodyFontSize;
        double micro = DesktopFontScale.Micro(body, 1d);
        double caption = DesktopFontScale.Caption(body, 1d);

        Assert.True(micro < caption, $"角标({micro}) 应小于农历({caption})。");
        Assert.True(caption < body, $"农历({caption}) 应小于正文({body})。");
    }

    /// <summary>
    /// 这是修复的核心：调大字体或缩放时，紧凑档必须**跟着变大**。
    /// 修复前它们与设置完全无关。
    /// </summary>
    [Fact]
    public void CompactTiersFollowTheFontSizeSetting()
    {
        double baselineMicro = DesktopFontScale.Micro(DefaultBodyFontSize, 1d);
        double baselineCaption = DesktopFontScale.Caption(DefaultBodyFontSize, 1d);

        double largerMicro = DesktopFontScale.Micro(20d, 1d);
        double largerCaption = DesktopFontScale.Caption(20d, 1d);

        Assert.True(largerMicro > baselineMicro, "字号调大后角标未跟随。");
        Assert.True(largerCaption > baselineCaption, "字号调大后农历未跟随。");
    }

    /// <summary>界面缩放同样要作用到紧凑档。</summary>
    [Fact]
    public void CompactTiersFollowTheScaleSetting()
    {
        double atOne = DesktopFontScale.Micro(DefaultBodyFontSize, 1d);
        double atOneAndHalf = DesktopFontScale.Micro(DefaultBodyFontSize, 1.5d);
        double captionAtOne = DesktopFontScale.Caption(DefaultBodyFontSize, 1d);
        double captionAtOneAndHalf = DesktopFontScale.Caption(DefaultBodyFontSize, 1.5d);

        Assert.Equal(atOne * 1.5d, atOneAndHalf, 2);
        Assert.Equal(captionAtOne * 1.5d, captionAtOneAndHalf, 2);
    }

    /// <summary>缩放到很小时要有下限，避免文字小到不可读。</summary>
    [Fact]
    public void CompactTiersNeverFallBelowTheReadableFloor()
    {
        Assert.Equal(DesktopFontScale.MinimumFontSize, DesktopFontScale.Micro(6d, 0.5d), 2);
        Assert.Equal(DesktopFontScale.MinimumFontSize, DesktopFontScale.Caption(6d, 0.5d), 2);
    }
}
