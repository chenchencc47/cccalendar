namespace CcCalendar.Desktop.ViewModels;

public static class MarkdownTextFormatter
{
    public static string Bold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"**{text}**";
    }

    public static string Italic(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"*{text}*";
    }

    public static string Bullet(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"- {text}";
    }
}
