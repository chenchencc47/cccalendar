namespace CcCalendar.Desktop.Interactions;

/// <summary>
/// 交互叠加层与动效的**令牌名与取值约定**（P61-01）。
///
/// 叠加与过渡本身由 XAML 模板完成：模板里放一个覆盖整个控件的 <c>Border</c>，其
/// <c>Background</c> 绑定 <c>{DynamicResource InteractionHoverBrush}</c>、初始
/// <c>Opacity=0</c>；悬停/按下的模板触发器用 <c>Storyboard</c> 把 <c>Opacity</c>
/// 过渡到 1，移出/松开时过渡回 0。
///
/// 两个关键取舍：
/// 1. **动画 <c>Border.Opacity</c> 而不是 <c>Background</c>**。换色方案在动画期间必须持有
///    具体颜色，会丢掉 <c>DynamicResource</c> 引用，主题切换后动画目标即失效；而叠加
///    Border 没有子内容，改它自己的 Opacity 不会连带淡出任何东西。
/// 2. **叠加色带 alpha**（深浅两套：普通底色用深/白叠加、强调底色用白色叠加）。
///    半透明叠加在任意底色上都成立，无需为每种底色各配一套 hover 色。
///
/// 本类集中登记这些名字，供 XAML 与契约测试共用，避免字符串散落各处。
/// </summary>
public static class SmoothTint
{
    /// <summary>过渡时长令牌名（<c>Duration</c>）。</summary>
    public const string MotionDurationToken = "MotionFast";

    /// <summary>缓动令牌名（<c>EasingFunctionBase</c>）。</summary>
    public const string MotionEasingToken = "EasingStandard";

    /// <summary>普通底色上的悬停叠加色令牌。</summary>
    public const string HoverBrushKey = "InteractionHoverBrush";

    /// <summary>普通底色上的按下叠加色令牌。</summary>
    public const string PressedBrushKey = "InteractionPressedBrush";

    /// <summary>强调色底上的悬停叠加色令牌（白色叠加）。</summary>
    public const string HoverOnAccentBrushKey = "InteractionHoverLightBrush";

    /// <summary>强调色底上的按下叠加色令牌（白色叠加）。</summary>
    public const string PressedOnAccentBrushKey = "InteractionPressedLightBrush";

    /// <summary>叠加层的初始不透明度（不可见）。</summary>
    public const double IdleOpacity = 0d;

    /// <summary>悬停/按下时叠加层的目标不透明度。</summary>
    public const double ActiveOpacity = 1d;

    /// <summary>全部交互叠加色令牌，供契约测试遍历断言。</summary>
    public static readonly string[] OverlayBrushKeys =
    [
        HoverBrushKey,
        PressedBrushKey,
        HoverOnAccentBrushKey,
        PressedOnAccentBrushKey,
    ];
}
