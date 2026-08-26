namespace CcCalendar.Core.Sync;

public sealed record OidcClientOptions(
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    string ClientId,
    Uri RedirectUri,
    IReadOnlyList<string> Scopes);
