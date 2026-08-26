namespace CcCalendar.Core.Sync;

public sealed record OidcTokenSet(
    string AccessToken,
    string? RefreshToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc);
