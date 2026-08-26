using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CcCalendar.Core.AI;
using CcCalendar.Desktop.ViewModels;
using CcCalendar.Desktop.Views;

namespace CcCalendar.Desktop.Tests.Views;

public sealed class AssistantViewRuntimeTests
{
    [Theory]
    [InlineData(1366, 768)]
    [InlineData(1920, 1080)]
    public void AssistantViewRendersWithoutCollapsedContentOrHorizontalOverflow(int width, int height)
    {
        RunOnSta(() =>
        {
            var viewModel = new AssistantViewModel(new RuntimeConfirmationService());
            var thinking = new AssistantMessageViewModel(false, "模型回复：会议已整理。", thinkingText: "正在读取日程并检查冲突……");
            viewModel.Messages.Add(new AssistantMessageViewModel(true, "请整理今天的会议和待办"));
            viewModel.Messages.Add(thinking);
            viewModel.PresentDraft(AiCreationDraft.Todo("发布前检查", null));

            var view = new AssistantView { DataContext = viewModel, Width = width, Height = height };
            var window = new Window { Content = view, Width = width, Height = height, ShowInTaskbar = false };
            try
            {
                window.Show();
                DoEvents(window);

                Assert.True(view.ActualWidth > 0 && view.ActualHeight > 0);
                Assert.True(view.ActualWidth <= width + 1, $"助理视图横向溢出：{view.ActualWidth} > {width}");
                Assert.True(view.ActualHeight <= height + 1, $"助理视图纵向溢出：{view.ActualHeight} > {height}");
                Assert.Contains(FindVisualChildren<Expander>(view), expander => expander.Header?.ToString() == "思考");

                var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(view);
                Assert.True(bitmap.PixelWidth == width && bitmap.PixelHeight == height);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void DoEvents(Window window)
    {
        for (int i = 0; i < 3; i++)
        {
            window.Dispatcher.Invoke(DispatcherPriority.Loaded, () => { });
            window.Dispatcher.Invoke(DispatcherPriority.Render, () => { });
            window.UpdateLayout();
        }
    }

    private static void RunOnSta(Action action) => WpfRuntimeHost.Run(action);

    private sealed class RuntimeConfirmationService : IAiCreationConfirmationService
    {
        public AiCreationPreview Preview(AiCreationDraft draft) => new(
            Guid.NewGuid(),
            draft.Kind,
            draft.Title,
            [new AiPreviewField("类型", "待办"), new AiPreviewField("内容", draft.Content ?? "")]);

        public Task<AiCreationResult> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCreationResult(Guid.NewGuid(), AiCreationKind.Todo));

        public void Cancel(Guid proposalId)
        {
        }
    }
}
