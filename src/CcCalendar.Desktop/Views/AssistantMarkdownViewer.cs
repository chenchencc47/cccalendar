using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MdXaml;

namespace CcCalendar.Desktop.Views;

public sealed class AssistantMarkdownViewer : MarkdownScrollViewer
{
    private static readonly FontFamily ChineseUiFont = new("Microsoft YaHei UI");

    public AssistantMarkdownViewer()
    {
        FontFamily = ChineseUiFont;
        FontSize = 14;
        MarkdownStyleName = "DocumentStyleCompact";
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == MarkdownProperty && Document is not null)
        {
            Document.FontFamily = ChineseUiFont;
            Document.FontSize = FontSize;
            NormalizeHeadings(Document.Blocks);
        }
    }

    private static void NormalizeHeadings(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            double compactSize = block.FontSize switch
            {
                >= 40 => 20,
                >= 30 => 18,
                >= 20 => 16,
                _ => block.FontSize,
            };
            if (compactSize != block.FontSize)
            {
                block.FontSize = compactSize;
                block.Margin = new Thickness(0, 8, 0, 4);
            }

            if (block is Section section)
            {
                NormalizeHeadings(section.Blocks);
            }
        }
    }
}
