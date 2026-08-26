using CcCalendar.Desktop.Desktop;

namespace CcCalendar.Desktop.Tests.Desktop;

public sealed class DesktopSizeEditorStateTests
{
    [Theory]
    [InlineData("760", "430", 760, 430)]
    [InlineData("100", "80", 220, 160)]
    [InlineData("5000", "4000", 1920, 1200)]
    public void ParseClampsToUsableWindowRange(
        string widthText,
        string heightText,
        double expectedWidth,
        double expectedHeight)
    {
        DesktopWindowSize size = DesktopSizeEditorState.Parse(widthText, heightText);

        Assert.Equal(new DesktopWindowSize(expectedWidth, expectedHeight), size);
    }
}
