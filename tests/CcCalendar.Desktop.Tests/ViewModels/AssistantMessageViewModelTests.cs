using CcCalendar.Core.AI;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

/// <summary>
/// P70-02：思考折叠行（ReasoningRow）的标题派生逻辑。
/// 标题有三种形态：流式中「思考中…」、完成「已深度思考（N 字·M 秒）」、
/// 历史恢复（无耗时记录）「已深度思考（N 字）」。
/// </summary>
public sealed class AssistantMessageViewModelTests
{
    [Fact]
    public void ReasoningStreamingShowsThinkingTitleWhileReceivingDeltas()
    {
        var clock = new AdjustableTimeProvider();
        var message = new AssistantMessageViewModel(false, string.Empty, timeProvider: clock);

        Assert.False(message.IsReasoningStreaming);
        Assert.False(message.HasThinking);

        message.AppendThinking("正在检查");

        Assert.True(message.IsReasoningStreaming);
        Assert.True(message.HasThinking);
        Assert.Equal("思考中…", message.ThinkingTitle);
    }

    [Fact]
    public void CompletedReasoningTitleContainsCharacterCountAndSeconds()
    {
        var clock = new AdjustableTimeProvider();
        var message = new AssistantMessageViewModel(false, string.Empty, timeProvider: clock);

        message.AppendThinking("abc");
        clock.Advance(TimeSpan.FromSeconds(5));
        message.EndReasoning();

        Assert.False(message.IsReasoningStreaming);
        Assert.Equal(TimeSpan.FromSeconds(5), message.ThinkingDuration);
        Assert.Contains("3 字", message.ThinkingTitle, StringComparison.Ordinal);
        Assert.Contains("5 秒", message.ThinkingTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void SubSecondReasoningDurationIsCountedAsOneSecond()
    {
        var clock = new AdjustableTimeProvider();
        var message = new AssistantMessageViewModel(false, string.Empty, timeProvider: clock);

        message.AppendThinking("想");
        clock.Advance(TimeSpan.FromMilliseconds(200));
        message.EndReasoning();

        // 不足 1 秒按 1 秒计，避免出现「0 秒」这种怪标题。
        Assert.Contains("1 秒", message.ThinkingTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoredReasoningWithoutDurationOnlyShowsCharacterCount()
    {
        // 聊天历史只持久化 ThinkingText，不持久化耗时：恢复的消息标题退化为只显示字数。
        var message = new AssistantMessageViewModel(false, "回答", thinkingText: "推理过程");

        Assert.False(message.IsReasoningStreaming);
        Assert.Null(message.ThinkingDuration);
        Assert.Contains("4 字", message.ThinkingTitle, StringComparison.Ordinal);
        Assert.DoesNotContain("秒", message.ThinkingTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyThinkingTextKeepsRowInvisible()
    {
        var message = new AssistantMessageViewModel(false, "回答");

        Assert.False(message.HasThinking);
        message.EndReasoning();
        Assert.False(message.HasThinking);
    }

    /// <summary>
    /// P70-04：消息级用量胶囊文本用 k 单位缩写（3245 → 3.2k）；
    /// 无用量时为 null（胶囊隐藏，用户消息与历史恢复消息都没有 usage）。
    /// </summary>
    [Fact]
    public void UsageSummaryFormatsTokensWithKUnits()
    {
        var message = new AssistantMessageViewModel(false, "回答");

        Assert.False(message.HasUsage);
        Assert.Null(message.UsageSummary);

        message.MergeUsage(new AiUsageUpdate(1000, 2245, CachedPromptTokens: 400, CacheWriteTokens: 7, Model: "test-model"));

        Assert.True(message.HasUsage);
        Assert.Equal("∑ 3.2k tokens", message.UsageSummary);
    }

    /// <summary>P70-04：小数值不缩写；弹窗行按四桶展开（未缓存输入 = 输入 − 缓存读）。</summary>
    [Fact]
    public void UsageSummaryKeepsSmallCountsAndDetailListsFourBuckets()
    {
        var message = new AssistantMessageViewModel(false, "回答");
        message.MergeUsage(new AiUsageUpdate(100, 20, 40));

        Assert.Equal("∑ 120 tokens", message.UsageSummary);
        Assert.Equal(
        [
            new AiPreviewField("未缓存输入", "60"),
            new AiPreviewField("缓存读", "40"),
            new AiPreviewField("缓存写", "0"),
            new AiPreviewField("输出", "20"),
        ],
        message.UsageDetail);
    }

    /// <summary>P70-04：模型名非空时追加到弹窗行尾（模型名以最新非空者为准）。</summary>
    [Fact]
    public void UsageDetailAppendsModelWhenReported()
    {
        var message = new AssistantMessageViewModel(false, "回答");
        message.MergeUsage(new AiUsageUpdate(100, 20, Model: "deepseek-chat"));

        Assert.Contains(message.UsageDetail, row => row is { Label: "模型", Value: "deepseek-chat" });
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan delta) => now += delta;
    }
}
