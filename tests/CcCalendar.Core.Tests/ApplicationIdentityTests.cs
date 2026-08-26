namespace CcCalendar.Core.Tests;

public sealed class ApplicationIdentityTests
{
    [Fact]
    public void ProductNameIsCccalendar()
    {
        Assert.Equal("cccalendar", ApplicationIdentity.ProductName);
    }
}
