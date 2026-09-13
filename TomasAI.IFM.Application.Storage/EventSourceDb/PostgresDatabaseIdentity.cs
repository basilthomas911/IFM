using Npgsql;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

public readonly record struct PostgresDatabaseIdentity(string Host, int Port, string Database, string Username)
{
    public static PostgresDatabaseIdentity Parse(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var value = new NpgsqlConnectionStringBuilder(connectionString);
        var hosts = value.Host.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length != 1)
            throw new InvalidOperationException("Atomic PostgreSQL transactions require one explicit database host.");
        return new(hosts[0].ToLowerInvariant(), value.Port, value.Database.ToLowerInvariant(), value.Username.ToLowerInvariant());
    }

    public static void RequireSamePhysicalDatabase(IDbConnectionSettings settings, string firstName, string secondName)
    {
        var first = settings[firstName] ?? throw new InvalidOperationException($"Connection setting '{firstName}' is required.");
        var second = settings[secondName] ?? throw new InvalidOperationException($"Connection setting '{secondName}' is required.");
        if (!first.ProviderName.Equals("System.Data.Postgres", StringComparison.Ordinal)
            || !second.ProviderName.Equals("System.Data.Postgres", StringComparison.Ordinal))
            throw new InvalidOperationException("Atomic Portfolio completion requires PostgreSQL for both PortfolioDb and EventSourceDb.");
        if (Parse(first.ConnectionString) != Parse(second.ConnectionString))
            throw new InvalidOperationException("PortfolioDb and EventSourceDb must identify the same physical PostgreSQL database and security identity.");
    }
}
