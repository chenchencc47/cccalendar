using System.Security.Cryptography;
using System.Text;
using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public static class PkceAuthorizationRequestFactory
{
    public static PkceAuthorizationRequest Create(
        OidcClientOptions options,
        string? state = null,
        string? codeVerifier = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientId);
        if (options.Scopes.Count == 0)
        {
            throw new ArgumentException("OIDC requires at least one scope.", nameof(options));
        }

        string requestState = state ?? CreateRandomValue(32);
        string verifier = codeVerifier ?? CreateRandomValue(32);
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string query = string.Join(
            '&',
            Parameter("response_type", "code"),
            Parameter("client_id", options.ClientId),
            Parameter("redirect_uri", options.RedirectUri.ToString()),
            Parameter("scope", string.Join(' ', options.Scopes)),
            Parameter("state", requestState),
            Parameter("code_challenge", challenge),
            Parameter("code_challenge_method", "S256"));

        UriBuilder builder = new(options.AuthorizationEndpoint)
        {
            Query = query,
        };
        return new PkceAuthorizationRequest(builder.Uri, requestState, verifier, challenge);
    }

    private static string CreateRandomValue(int byteCount)
    {
        return Base64Url(RandomNumberGenerator.GetBytes(byteCount));
    }

    private static string Parameter(string name, string value)
    {
        return $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
