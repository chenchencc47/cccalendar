using System.Windows;
using System.Windows.Documents;
using CcCalendar.Desktop.Views;
using MdXaml;

namespace CcCalendar.Desktop.Tests.Views;

public sealed class AssistantMarkdownRenderingTests
{
    [Fact]
    public void MarkdownViewerParsesFormattingWithoutLeavingLiteralMarkers()
    {
        Exception? failure = null;
        string? renderedText = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewer = new MarkdownScrollViewer
                {
                    Markdown = "# 标题\n\n**重点**\n\n- 第一项\n\n`code`",
                };
                FlowDocument document = viewer.Document
                    ?? throw new InvalidOperationException("Markdown document was not created.");
                renderedText = new TextRange(
                    document.ContentStart,
                    document.ContentEnd).Text;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.NotNull(renderedText);
        Assert.Contains("标题", renderedText, StringComparison.Ordinal);
        Assert.Contains("重点", renderedText, StringComparison.Ordinal);
        Assert.Contains("第一项", renderedText, StringComparison.Ordinal);
        Assert.Contains("code", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("#", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("**", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("`", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantMarkdownUsesCompactChineseTypography()
    {
        Exception? failure = null;
        string? viewerFont = null;
        string? documentFont = null;
        double headingSize = double.MaxValue;
        var thread = new Thread(() =>
        {
            try
            {
                var viewer = new AssistantMarkdownViewer
                {
                    Markdown = "# 每日计划\n\n正文",
                };
                viewerFont = viewer.FontFamily.Source;
                FlowDocument document = viewer.Document
                    ?? throw new InvalidOperationException("Markdown document was not created.");
                documentFont = document.FontFamily.Source;
                headingSize = document.Blocks.First().FontSize;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.Equal("Microsoft YaHei UI", viewerFont);
        Assert.Equal("Microsoft YaHei UI", documentFont);
        Assert.InRange(headingSize, 16, 20);
    }
}
