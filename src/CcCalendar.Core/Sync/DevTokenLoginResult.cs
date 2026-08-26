namespace CcCalendar.Core.Sync;

public sealed record DevTokenLoginResult(
    OidcTokenSet Tokens,
    Guid UserId,
    Guid WorkspaceId);
