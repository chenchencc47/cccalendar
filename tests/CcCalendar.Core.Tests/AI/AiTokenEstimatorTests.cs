using CcCalendar.Core.AI;

namespace CcCalendar.Core.Tests.AI;

public sealed class AiTokenEstimatorTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abc", 1)]
    [InlineData("一二三", 2)]
    [InlineData("一二", 2)]
    [InlineData("a一b二", 2)]
    public void EstimateTokensMatchesHeuristic(string text, int expected)
    {
        Assert.Equal(expected, AiTokenEstimator.EstimateTokens(text));
    }

    [Fact]
    public void EstimateCompositionSplitsContextSegments()
    {
        var context = new AiRequestContext(
            "系统指令",
            "用户问题",
            [new AiToolDefinition("tool_a", "查询工具", "{}", true)],
            [
                new AiAssistantTurn(true, "第一问"),
                new AiAssistantTurn(false, "第一答"),
            ]);

        AiContextCompositionUpdate composition = AiTokenEstimator.EstimateComposition(context);

        Assert.Equal(AiTokenEstimator.EstimateTokens("系统指令"), composition.SystemTokens);
        Assert.True(composition.ToolSchemaTokens > 0);
        Assert.Equal(
            AiTokenEstimator.EstimateTokens("第一问") + AiTokenEstimator.EstimateTokens("用户问题"),
            composition.UserTokens);
        Assert.Equal(AiTokenEstimator.EstimateTokens("第一答"), composition.AssistantTokens);
    }

    [Fact]
    public void EstimateCompositionWithoutToolsOrConversationStillProducesSegments()
    {
        var context = new AiRequestContext("指令", "问题", [], []);

        AiContextCompositionUpdate composition = AiTokenEstimator.EstimateComposition(context);

        Assert.True(composition.SystemTokens > 0);
        Assert.Equal(0, composition.ToolSchemaTokens);
        Assert.Equal(AiTokenEstimator.EstimateTokens("问题"), composition.UserTokens);
        Assert.Equal(0, composition.AssistantTokens);
    }
}
