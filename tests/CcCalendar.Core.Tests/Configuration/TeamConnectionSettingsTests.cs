using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.Tests.Configuration;

public sealed class TeamConnectionSettingsTests
{
    [Fact]
    public void TokenIdentifierUsesDevTokensWhenOidcEndpointsAreMissing()
    {
        var settings = new TeamConnectionSettings
        {
            ApiBaseUrl = "http://192.168.1.88:5080/",
            DevTokenName = "张三",
        };

        Assert.Equal("dev.tokens", settings.ResolveTokenIdentifier());

        Assert.Equal(
            "dev.tokens",
            (settings with { TokenEndpoint = "https://idp.example.com/token" }).ResolveTokenIdentifier());
    }

    [Fact]
    public void TokenIdentifierUsesOidcTokensWithClientSuffix()
    {
        var settings = new TeamConnectionSettings
        {
            AuthorizationEndpoint = "https://idp.example.com/authorize",
            TokenEndpoint = "https://idp.example.com/token",
            ClientId = " cccalendar ",
        };

        Assert.Equal("oidc.tokens.cccalendar", settings.ResolveTokenIdentifier());
        Assert.Equal(
            "oidc.tokens",
            (settings with { ClientId = string.Empty }).ResolveTokenIdentifier());
    }
}
