using CcCalendar.Core.Records;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class RecordWorkspaceViewModel : ObservableObject
{
    private readonly TimeProvider timeProvider;
    private IReadOnlyList<WorkRecord> allRecords = [];
    private IReadOnlyList<WorkRecord> visibleRecords = [];
    private WorkRecord? selectedRecord;
    private string searchText = string.Empty;
    private string editorContent = string.Empty;

    public RecordWorkspaceViewModel(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public IReadOnlyList<WorkRecord> VisibleRecords
    {
        get => visibleRecords;
        private set => SetProperty(ref visibleRecords, value);
    }

    public WorkRecord? SelectedRecord
    {
        get => selectedRecord;
        set
        {
            if (SetProperty(ref selectedRecord, value))
            {
                EditorContent = value?.CurrentContent ?? string.Empty;
            }
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty))
            {
                RefreshSearch();
            }
        }
    }

    public string EditorContent
    {
        get => editorContent;
        set => SetProperty(ref editorContent, value ?? string.Empty);
    }

    public void Load(IEnumerable<WorkRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        allRecords = [.. records.OrderByDescending(record => record.UpdatedAtUtc)];
        RefreshSearch();
        SelectedRecord = VisibleRecords.Count > 0 ? VisibleRecords[0] : null;
    }

    public bool SaveEditorContent()
    {
        return SelectedRecord?.SaveRevision(EditorContent, timeProvider.GetUtcNow()) ?? false;
    }

    private void RefreshSearch()
    {
        string query = SearchText.Trim();
        VisibleRecords = query.Length == 0
            ? allRecords
            : [.. allRecords.Where(record =>
                record.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || record.CurrentContent.Contains(query, StringComparison.OrdinalIgnoreCase))];

        if (SelectedRecord is not null && !VisibleRecords.Contains(SelectedRecord))
        {
            SelectedRecord = VisibleRecords.Count > 0 ? VisibleRecords[0] : null;
        }
    }
}
