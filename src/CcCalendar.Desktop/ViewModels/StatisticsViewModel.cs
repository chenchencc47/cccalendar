using CcCalendar.Core.Projects;
using CcCalendar.Core.Todos;
using CcCalendar.Core.Tools;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class StatisticsViewModel : ObservableObject
{
    private readonly TimeProvider timeProvider;

    public StatisticsViewModel(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public int ProjectCount { get; private set; }

    public int TotalTodos { get; private set; }

    public int CompletedTodos { get; private set; }

    public int OverdueTodos { get; private set; }

    public int CompletionPercent { get; private set; }

    public int FocusMinutes { get; private set; }

    public IReadOnlyList<DailyCompletionPointViewModel> DailyCompletions { get; private set; } = [];

    public string WeeklySummary { get; private set; } = "本周暂无任务数据";

    public void Load(
        IEnumerable<Project> projects,
        IEnumerable<TodoItem> todos,
        IEnumerable<FocusSession>? focusSessions = null)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(todos);

        Project[] projectItems = [.. projects];
        TodoItem[] todoItems = [.. todos];
        FocusSession[] sessionItems = [.. focusSessions ?? []];
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DateOnly firstDay = today.AddDays(-6);
        ProjectCount = projectItems.Length;
        TotalTodos = todoItems.Length;
        CompletedTodos = todoItems.Count(todo => todo.Status == TodoStatus.Completed);
        OverdueTodos = todoItems.Count(todo =>
            todo.Status != TodoStatus.Completed
            && todo.DueAtUtc.HasValue
            && todo.DueAtUtc < nowUtc);
        CompletionPercent = TotalTodos == 0 ? 0 : CompletedTodos * 100 / TotalTodos;
        FocusMinutes = sessionItems
            .Where(session => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                session.StartedAtUtc,
                timeProvider.LocalTimeZone).DateTime) >= firstDay)
            .Sum(session => session.DurationMinutes);

        int[] dailyCounts = new int[7];

        foreach (TodoItem todo in todoItems.Where(item => item.CompletedAtUtc.HasValue))
        {
            DateTimeOffset localCompletion = TimeZoneInfo.ConvertTime(
                todo.CompletedAtUtc!.Value,
                timeProvider.LocalTimeZone);
            int dayIndex = DateOnly.FromDateTime(localCompletion.DateTime).DayNumber - firstDay.DayNumber;

            if (dayIndex is >= 0 and < 7)
            {
                dailyCounts[dayIndex]++;
            }
        }

        int maximum = Math.Max(1, dailyCounts.Max());
        DailyCompletions = [.. Enumerable.Range(0, 7).Select(index =>
            new DailyCompletionPointViewModel(
                firstDay.AddDays(index),
                dailyCounts[index],
                Math.Max(2, dailyCounts[index] * 80d / maximum)))];
        int weeklyCompleted = dailyCounts.Sum();
        WeeklySummary = $"本周完成 {weeklyCompleted} 项，当前逾期 {OverdueTodos} 项，整体完成率 {CompletionPercent}%。";

        OnPropertyChanged(nameof(ProjectCount));
        OnPropertyChanged(nameof(TotalTodos));
        OnPropertyChanged(nameof(CompletedTodos));
        OnPropertyChanged(nameof(OverdueTodos));
        OnPropertyChanged(nameof(CompletionPercent));
        OnPropertyChanged(nameof(FocusMinutes));
        OnPropertyChanged(nameof(DailyCompletions));
        OnPropertyChanged(nameof(WeeklySummary));
    }
}
