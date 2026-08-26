using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.AI;

public sealed record AiConnectionTestResult(bool Success, string Message);

public interface IAiConnectionTester
{
    Task<AiConnectionTestResult> TestAsync(
        AiProviderSettings settings,
        string? apiKey,
        CancellationToken cancellationToken);
}
