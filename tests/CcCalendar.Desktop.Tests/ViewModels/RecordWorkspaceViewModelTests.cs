using CcCalendar.Core.Records;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class RecordWorkspaceViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SearchMatchesTitleAndCurrentContent()
    {
        WorkRecord meeting = WorkRecord.Create(WorkRecordType.Meeting, "接口会议", null, "讨论鉴权", Now);
        WorkRecord log = WorkRecord.Create(WorkRecordType.WorkLog, "工作日志", null, "完成日历网格", Now);
        var viewModel = new RecordWorkspaceViewModel(new FixedTimeProvider(Now));
        viewModel.Load([meeting, log]);

        viewModel.SearchText = "鉴权";

        Assert.Single(viewModel.VisibleRecords, meeting);
    }

    [Fact]
    public void SaveEditorContentAppendsVersionOnlyWhenChanged()
    {
        WorkRecord record = WorkRecord.Create(WorkRecordType.WorkLog, "日志", null, "初始", Now);
        var viewModel = new RecordWorkspaceViewModel(new FixedTimeProvider(Now.AddMinutes(1)));
        viewModel.Load([record]);
        viewModel.EditorContent = "更新";

        bool saved = viewModel.SaveEditorContent();
        bool savedAgain = viewModel.SaveEditorContent();

        Assert.True(saved);
        Assert.False(savedAgain);
        Assert.Equal("更新", record.CurrentContent);
        Assert.Equal(2, record.Versions.Count);
    }

    [Fact]
    public void MarkdownFormatterWrapsSelection()
    {
        Assert.Equal("**重点**", MarkdownTextFormatter.Bold("重点"));
        Assert.Equal("- 待办", MarkdownTextFormatter.Bullet("待办"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
