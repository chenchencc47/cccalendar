using System.Text.Json;
using CcCalendar.Core.AI;

namespace CcCalendar.Core.Tests.AI;

public sealed class ReadOnlyAiToolCatalogTests
{
    [Fact]
    public void CatalogContainsOnlyReadToolsAndContextDoesNotEmbedApplicationData()
    {
        var catalog = new ReadOnlyAiToolCatalog();
        AiRequestContext context = MinimalAiContextBuilder.Build("查找发布待办", catalog.Definitions);
        string json = JsonSerializer.Serialize(context);

        Assert.Equal(
            ["query_events", "query_projects", "query_records", "query_todos"],
            catalog.Definitions.Select(tool => tool.Name).Order());
        Assert.All(catalog.Definitions, tool => Assert.True(tool.IsReadOnly));
        Assert.DoesNotContain("create", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delete", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-database-content", json, StringComparison.Ordinal);
        Assert.Equal("查找发布待办", context.UserMessage);
    }

    [Fact]
    public void BuildWithLocalNowEmbedsCurrentDateAndNoAskBackRule()
    {
        var catalog = new ReadOnlyAiToolCatalog();
        var wednesday = new DateTimeOffset(2026, 8, 19, 18, 27, 0, TimeSpan.FromHours(8));

        AiRequestContext context = MinimalAiContextBuilder.Build(
            "本周一到周四添加每日例会",
            catalog.Definitions,
            AiAssistantMode.Write,
            wednesday);

        Assert.Contains("2026-08-19（周三）18:27", context.SystemInstruction, StringComparison.Ordinal);
        Assert.Contains("不要再反问用户", context.SystemInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWithoutLocalNowKeepsInstructionDateFree()
    {
        var catalog = new ReadOnlyAiToolCatalog();

        AiRequestContext context = MinimalAiContextBuilder.Build(
            "查找发布待办",
            catalog.Definitions);

        Assert.DoesNotContain("当前本地时间", context.SystemInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRetainsOnlyTheMostRecentConversationTurns()
    {
        var catalog = new ReadOnlyAiToolCatalog();
        AiAssistantTurn[] turns = [..
            Enumerable.Range(1, 41)
                .Select(index => new AiAssistantTurn(index % 2 == 1, $"第{index}轮"))];

        AiRequestContext context = MinimalAiContextBuilder.Build(
            "当前问题",
            catalog.Definitions,
            conversation: turns);

        Assert.Equal(40, context.Conversation.Count);
        Assert.Equal("第2轮", context.Conversation[0].Text);
        Assert.Equal("第41轮", context.Conversation[^1].Text);
    }
}
