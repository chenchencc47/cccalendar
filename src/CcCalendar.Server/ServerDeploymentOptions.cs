using Microsoft.Extensions.Configuration;

namespace CcCalendar.Server;

public sealed record ServerDeploymentOptions(
    string StorageProvider,
    string DataDirectory,
    string? PostgresConnectionString,
    bool RequireHttps)
{
    public static ServerDeploymentOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string provider = configuration["Storage:Provider"]?.Trim().ToLowerInvariant() ?? "file";
        string dataDirectory = configuration["Storage:DataDirectory"]?.Trim()
            ?? Path.Combine(AppContext.BaseDirectory, "data");
        string? connectionString = configuration["Storage:ConnectionString"]
            ?? configuration.GetConnectionString("Postgres");
        bool requireHttps = bool.TryParse(
            configuration["Network:RequireHttps"],
            out bool parsedRequireHttps)
            && parsedRequireHttps;
        return new ServerDeploymentOptions(provider, dataDirectory, connectionString, requireHttps);
    }

    public void Validate()
    {
        if (StorageProvider is not ("file" or "postgres"))
        {
            throw new InvalidOperationException(
                $"Unsupported storage provider '{StorageProvider}'. Use 'file' or 'postgres'.");
        }

        if (StorageProvider == "postgres" && string.IsNullOrWhiteSpace(PostgresConnectionString))
        {
            throw new InvalidOperationException(
                "Storage:ConnectionString is required when Storage:Provider is 'postgres'.");
        }
    }
}
