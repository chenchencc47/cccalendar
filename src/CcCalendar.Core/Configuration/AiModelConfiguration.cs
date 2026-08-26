namespace CcCalendar.Core.Configuration;

public sealed record AiModelConfiguration(
    string Id,
    string DisplayName,
    AiProviderSettings Settings);
