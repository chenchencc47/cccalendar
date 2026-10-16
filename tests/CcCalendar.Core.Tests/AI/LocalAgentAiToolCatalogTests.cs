using System.Text.Json;
using CcCalendar.Core.AI;
using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.Tests.AI;

public sealed class LocalAgentAiToolCatalogTests
{
    [Fact]
    public void DefinitionsContainFiveLocalToolsWithReadOnlyFlags()
    {
        var catalog = new LocalAgentAiToolCatalog();

        Assert.Equal(
            ["read_file", "list_directory", "search_files", "write_file", "run_command"],
            catalog.Definitions.Select(definition => definition.Name));
        Assert.Equal(
            [true, true, true, false, false],
            catalog.Definitions.Select(definition => definition.IsReadOnly));
        Assert.All(
            catalog.Definitions,
            definition => Assert.False(string.IsNullOrWhiteSpace(definition.ParametersJsonSchema)));
    }

    [Fact]
    public void GetAllowedToolsReturnsReadToolsByDefault()
    {
        var catalog = new LocalAgentAiToolCatalog();

        IReadOnlyList<AiToolDefinition> allowed = catalog.GetAllowedTools(new LocalAgentSettings());

        Assert.Equal(
            ["read_file", "list_directory", "search_files"],
            allowed.Select(definition => definition.Name));
    }

    [Fact]
    public void GetAllowedToolsRespectsEverySwitch()
    {
        var catalog = new LocalAgentAiToolCatalog();
        var settings = new LocalAgentSettings
        {
            Enabled = true,
            AllowReadFile = false,
            AllowListDirectory = false,
            AllowSearchFiles = false,
            AllowWriteFile = true,
            AllowRunCommand = true,
        };

        IReadOnlyList<AiToolDefinition> allowed = catalog.GetAllowedTools(settings);

        Assert.Equal(
            ["write_file", "run_command"],
            allowed.Select(definition => definition.Name));
    }

    [Theory]
    [InlineData("read_file", true)]
    [InlineData("list_directory", true)]
    [InlineData("search_files", true)]
    [InlineData("write_file", true)]
    [InlineData("run_command", true)]
    [InlineData("spawn_subagent", false)]
    [InlineData("query_events", false)]
    [InlineData("propose_todo", false)]
    public void IsLocalToolRecognizesOnlyLocalToolNames(string toolName, bool expected)
    {
        Assert.Equal(expected, LocalAgentAiToolCatalog.IsLocalTool(toolName));
    }

    [Fact]
    public void SpawnSubagentToolDefinitionCarriesTaskParameters()
    {
        Assert.Equal("spawn_subagent", LocalAgentAiToolCatalog.SpawnSubagentTool.Name);
        Assert.Equal(
            ["description", "task"],
            ExtractRequiredProperties(LocalAgentAiToolCatalog.SpawnSubagentTool.ParametersJsonSchema));
        Assert.False(LocalAgentAiToolCatalog.SpawnSubagentTool.IsReadOnly);
        Assert.Equal(2, LocalAgentAiToolCatalog.MaximumSubagentDepth);
        return;

        static List<string> ExtractRequiredProperties(string schema)
        {
            using JsonDocument document = JsonDocument.Parse(schema);
            return [.. document.RootElement.GetProperty("required").EnumerateArray()
                .Select(property => property.GetString()!)];
        }
    }

    [Theory]
    [InlineData("write_file", true)]
    [InlineData("run_command", true)]
    [InlineData("read_file", false)]
    [InlineData("list_directory", false)]
    [InlineData("search_files", false)]
    [InlineData("spawn_subagent", false)]
    [InlineData("query_events", false)]
    [InlineData("propose_todo", false)]
    public void RequiresApprovalCoversOnlyWriteTools(string toolName, bool expected)
    {
        Assert.Equal(expected, LocalAgentAiToolCatalog.RequiresApproval(toolName));
    }
}
