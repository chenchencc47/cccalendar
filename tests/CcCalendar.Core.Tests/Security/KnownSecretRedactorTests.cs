using CcCalendar.Core.Security;

namespace CcCalendar.Core.Tests.Security;

public sealed class KnownSecretRedactorTests
{
    [Fact]
    public void RedactReplacesEveryKnownSecret()
    {
        var redactor = new KnownSecretRedactor(["sk-calendar-123", "refresh-token-456"]);
        const string message = "Authorization: Bearer sk-calendar-123; refresh=refresh-token-456";

        string redacted = redactor.Redact(message);

        Assert.Equal("Authorization: Bearer [REDACTED]; refresh=[REDACTED]", redacted);
    }

    [Fact]
    public void EmptyValuesAreIgnored()
    {
        var redactor = new KnownSecretRedactor([string.Empty, "  ", "secret"]);

        string redacted = redactor.Redact("a secret remains protected");

        Assert.Equal("a [REDACTED] remains protected", redacted);
    }
}
