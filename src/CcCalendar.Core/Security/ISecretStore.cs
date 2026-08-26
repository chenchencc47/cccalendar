namespace CcCalendar.Core.Security;

public interface ISecretStore
{
    Task<string?> ReadAsync(string identifier, CancellationToken cancellationToken);

    Task WriteAsync(string identifier, string secret, CancellationToken cancellationToken);

    Task DeleteAsync(string identifier, CancellationToken cancellationToken);
}
