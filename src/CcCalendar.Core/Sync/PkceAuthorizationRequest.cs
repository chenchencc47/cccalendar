namespace CcCalendar.Core.Sync;

public sealed record PkceAuthorizationRequest(
    Uri AuthorizationUri,
    string State,
    string CodeVerifier,
    string CodeChallenge);
