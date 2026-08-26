namespace CcCalendar.Core.Security;

public sealed class KnownSecretRedactor
{
    private readonly string[] knownSecrets;

    public KnownSecretRedactor(IEnumerable<string> knownSecrets)
    {
        ArgumentNullException.ThrowIfNull(knownSecrets);

        this.knownSecrets = knownSecrets
            .Where(secret => !string.IsNullOrWhiteSpace(secret))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(secret => secret.Length)
            .ToArray();
    }

    public string Redact(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string redacted = message;

        foreach (string secret in knownSecrets)
        {
            redacted = redacted.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        }

        return redacted;
    }
}
