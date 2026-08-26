using System.Diagnostics;
using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public interface IOidcBrowserLauncher
{
    void Open(Uri authorizationUri);
}

public sealed class SystemBrowserLauncher : IOidcBrowserLauncher
{
    public void Open(Uri authorizationUri)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        Process.Start(new ProcessStartInfo
        {
            FileName = authorizationUri.ToString(),
            UseShellExecute = true,
        });
    }
}

public sealed class OidcLoginClient(
    OidcTokenClient tokenClient,
    IOidcBrowserLauncher browserLauncher)
{
    public async Task<OidcTokenSet> LoginAsync(
        OidcClientOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var callbackReceiver = new LoopbackOidcCallbackReceiver();
        OidcClientOptions callbackOptions = options with
        {
            RedirectUri = callbackReceiver.RedirectUri,
        };
        PkceAuthorizationRequest authorization = PkceAuthorizationRequestFactory.Create(callbackOptions);
        browserLauncher.Open(authorization.AuthorizationUri);
        Uri callback = await callbackReceiver.WaitAsync(cancellationToken);
        string code = OidcLoginFlow.RequireAuthorizationCode(callback, authorization.State);
        return await tokenClient.ExchangeCodeAsync(
            callbackOptions.TokenEndpoint,
            callbackOptions.ClientId,
            code,
            callbackOptions.RedirectUri.ToString(),
            authorization.CodeVerifier,
            cancellationToken);
    }
}
