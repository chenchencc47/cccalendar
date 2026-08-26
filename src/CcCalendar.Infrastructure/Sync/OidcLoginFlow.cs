using System.Security;

namespace CcCalendar.Infrastructure.Sync;

public static class OidcLoginFlow
{
    public static string RequireAuthorizationCode(Uri callbackUri, string expectedState)
    {
        ArgumentNullException.ThrowIfNull(callbackUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedState);
        Dictionary<string, string> parameters = ParseQuery(callbackUri.Query);

        if (!parameters.TryGetValue("state", out string? actualState)
            || !string.Equals(actualState, expectedState, StringComparison.Ordinal))
        {
            throw new SecurityException("The OIDC callback state did not match the login request.");
        }

        if (parameters.TryGetValue("error", out string? error))
        {
            string description = parameters.GetValueOrDefault("error_description") ?? string.Empty;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(description) ? error : $"{error}: {description}");
        }

        if (!parameters.TryGetValue("code", out string? code) || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("The OIDC callback did not contain an authorization code.");
        }

        return code;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => Uri.UnescapeDataString(parts[1].Replace('+', ' ')),
                StringComparer.Ordinal);
    }
}
