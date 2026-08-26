using System.Net.Sockets;
using System.Security;
using System.Text;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Infrastructure.Tests.Sync;

public sealed class OidcLoginFlowTests
{
    [Fact]
    public void CallbackWithMatchingStateReturnsAuthorizationCode()
    {
        Uri callback = new("http://127.0.0.1:49152/callback/?code=auth-code&state=state-1");

        string code = OidcLoginFlow.RequireAuthorizationCode(callback, "state-1");

        Assert.Equal("auth-code", code);
    }

    [Fact]
    public void CallbackWithDifferentStateIsRejected()
    {
        Uri callback = new("http://127.0.0.1:49152/callback/?code=auth-code&state=attacker");

        Assert.Throws<SecurityException>(
            () => OidcLoginFlow.RequireAuthorizationCode(callback, "state-1"));
    }

    [Fact]
    public void ProviderErrorIsSurfacedBeforeTokenExchange()
    {
        Uri callback = new("http://127.0.0.1:49152/callback/?error=access_denied&state=state-1");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => OidcLoginFlow.RequireAuthorizationCode(callback, "state-1"));

        Assert.Contains("access_denied", exception.Message);
    }

    [Fact]
    public async Task LoopbackReceiverAcceptsOnlyItsCallbackPath()
    {
        using var receiver = new LoopbackOidcCallbackReceiver();
        Task<Uri> callbackTask = receiver.WaitAsync(CancellationToken.None);
        using var client = new TcpClient();
        await client.ConnectAsync(receiver.RedirectUri.Host, receiver.RedirectUri.Port);
        await using NetworkStream stream = client.GetStream();
        byte[] request = Encoding.ASCII.GetBytes(
            $"GET {receiver.RedirectUri.AbsolutePath}?code=auth&state=state HTTP/1.1\r\nHost: localhost\r\n\r\n");
        await stream.WriteAsync(request);

        Uri callback = await callbackTask;

        Assert.Equal("auth", OidcLoginFlow.RequireAuthorizationCode(callback, "state"));
    }
}
