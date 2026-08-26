using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CcCalendar.Desktop.ViewModels;
using Microsoft.Win32;

namespace CcCalendar.Desktop.Views;

public partial class AssistantView : UserControl
{
    private AssistantViewModel? subscribedViewModel;
    private NotifyCollectionChangedEventHandler? messagesChangedHandler;

    public AssistantView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is AssistantViewModel viewModel)
            {
                AttachAutoScroll(viewModel);
            }

            ScrollToLatestMessage();
        };
        Unloaded += (_, _) => DetachAutoScroll();
        if (DataContext is AssistantViewModel viewModel)
        {
            AttachAutoScroll(viewModel);
        }

        DataContextChanged += (_, _) =>
        {
            DetachAutoScroll();
            if (DataContext is AssistantViewModel viewModel)
            {
                AttachAutoScroll(viewModel);
            }
        };
    }

    private void AttachAutoScroll(AssistantViewModel viewModel)
    {
        DetachAutoScroll();
        subscribedViewModel = viewModel;
        messagesChangedHandler = (_, _) => ScrollToLatestMessageIfNearBottom();
        viewModel.Messages.CollectionChanged += messagesChangedHandler;
    }

    private void DetachAutoScroll()
    {
        if (subscribedViewModel is not null && messagesChangedHandler is not null)
        {
            subscribedViewModel.Messages.CollectionChanged -= messagesChangedHandler;
        }

        subscribedViewModel = null;
        messagesChangedHandler = null;
    }

    private void ScrollToLatestMessage()
    {
        if (MessageList.Items.Count > 0)
        {
            MessageList.ScrollIntoView(MessageList.Items[^1]);
        }
    }

    private void ScrollToLatestMessageIfNearBottom()
    {
        ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(MessageList);
        if (scrollViewer is not null
            && scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset > 48)
        {
            return;
        }

        ScrollToLatestMessage();
    }

    private void MessageListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(MessageList);
        if (scrollViewer is null)
        {
            return;
        }

        double nextOffset = scrollViewer.VerticalOffset - e.Delta * 0.45;
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(nextOffset, 0, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void InputBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Shift+Enter：由 TextBox 自行插入换行（AcceptsReturn=True）。
            return;
        }

        // Enter：直接发送消息。
        if (DataContext is AssistantViewModel viewModel
            && viewModel.SendCommand.CanExecute(null))
        {
            viewModel.SendCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void CopyMessageClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string text }
            && !string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }

    private async void AttachFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "支持的文本文件|*.txt;*.md;*.csv;*.json;*.ics|所有文件|*.*",
            Multiselect = true,
            Title = "添加给 AI 助手",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true
            || DataContext is not AssistantViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.AddAttachmentsAsync(dialog.FileNames, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or InvalidDataException)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                exception.Message,
                "无法添加附件",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
