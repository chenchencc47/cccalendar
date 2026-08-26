using System.Text.Json;
using CcCalendar.Core.Security;
using CcCalendar.Core.Sync;

namespace CcCalendar.Infrastructure.Sync;

public sealed class OidcTokenStore(ISecretStore secretStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OidcTokenSet?> ReadAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        string? json = await secretStore.ReadAsync(identifier, cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<OidcTokenSet>(json, JsonOptions);
    }

    public Task WriteAsync(
        string identifier,
        OidcTokenSet tokens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return secretStore.WriteAsync(
            identifier,
            JsonSerializer.Serialize(tokens, JsonOptions),
            cancellationToken);
    }

    public Task DeleteAsync(string identifier, CancellationToken cancellationToken)
    {
        return secretStore.DeleteAsync(identifier, cancellationToken);
    }
}
