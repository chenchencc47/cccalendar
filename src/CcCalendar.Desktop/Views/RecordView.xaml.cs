using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Views;

public partial class RecordView : UserControl
{
    public event EventHandler? CreateRecordRequested;
    public event EventHandler<RecordSaveRequestedEventArgs>? SaveRequested;

    public RecordView()
    {
        InitializeComponent();
    }

    private void BoldClick(object sender, RoutedEventArgs e) => WrapSelection(MarkdownTextFormatter.Bold);

    private void ItalicClick(object sender, RoutedEventArgs e) => WrapSelection(MarkdownTextFormatter.Italic);

    private void BulletClick(object sender, RoutedEventArgs e) => WrapSelection(MarkdownTextFormatter.Bullet);

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RecordWorkspaceViewModel viewModel)
        {
            return;
        }

        if (viewModel.SelectedRecord is null)
        {
            SaveStatusText.Text = "请先选择记录";
            return;
        }

        SaveStatusText.Text = "正在保存...";
        SaveRequested?.Invoke(
            this,
            new RecordSaveRequestedEventArgs(
                viewModel.SelectedRecord.Id,
                viewModel.EditorContent,
                status => SaveStatusText.Text = status));
    }

    private void CreateRecordClick(object sender, RoutedEventArgs e)
    {
        CreateRecordRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        if (e.Key == Key.B)
        {
            WrapSelection(MarkdownTextFormatter.Bold);
            e.Handled = true;
        }
        else if (e.Key == Key.I)
        {
            WrapSelection(MarkdownTextFormatter.Italic);
            e.Handled = true;
        }
    }

    private void WrapSelection(Func<string, string> formatter)
    {
        string replacement = formatter(EditorBox.SelectedText);
        int selectionStart = EditorBox.SelectionStart;
        EditorBox.SelectedText = replacement;
        EditorBox.Select(selectionStart, replacement.Length);
        EditorBox.Focus();
    }
}

public sealed class RecordSaveRequestedEventArgs(
    Guid? recordId,
    string content,
    Action<string>? reportStatus = null) : EventArgs
{
    public Guid? RecordId { get; } = recordId;
    public string Content { get; } = content;

    public void ReportStatus(string status) => reportStatus?.Invoke(status);
}
