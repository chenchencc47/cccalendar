namespace CcCalendar.Core.AI;

public sealed record AiTextDraft(string Title, string Markdown);

public interface IAiDraftService
{
    Task<AiTextDraft> BuildProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken);

    Task<AiTextDraft> BuildDailyPlanAsync(
        DateOnly calendarDate,
        string timeZoneId,
        CancellationToken cancellationToken);

    Task<AiTextDraft> BuildWeeklyReportAsync(
        DateOnly weekStart,
        string timeZoneId,
        CancellationToken cancellationToken);
}
