using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void CreateBuildsExpectedNavigationAndSelectsToday()
    {
        var now = new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);
        var viewModel = new MainWindowViewModel(new FixedTimeProvider(now));

        Assert.Equal(
            ["今天", "日历", "项目", "待办", "记录", "助理", "统计", "工具", "设置"],
            viewModel.NavigationItems.Select(item => item.Label));
        Assert.Equal(NavigationDestination.Today, viewModel.SelectedItem.Destination);
        Assert.Equal("2026年8月16日 星期日", viewModel.Today.FormattedDate);
    }

    [Fact]
    public void SelectingNavigationItemChangesCurrentDestination()
    {
        var viewModel = new MainWindowViewModel(TimeProvider.System);
        NavigationItemViewModel projects = viewModel.NavigationItems.Single(
            item => item.Destination == NavigationDestination.Projects);

        viewModel.SelectedItem = projects;

        Assert.Equal(NavigationDestination.Projects, viewModel.SelectedItem.Destination);
        Assert.Equal("项目", viewModel.SelectedPageTitle);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
