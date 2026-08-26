namespace CcCalendar.Core.AI;

public sealed record AiToolDefinition(
    string Name,
    string Description,
    string ParametersJsonSchema,
    bool IsReadOnly);

public sealed record AiToolExecutionResult(string Json);
