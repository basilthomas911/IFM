using Npgsql;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct PostgresDatabaseIdentity(string Host, int Port, string Database, string Username)
{
    public static PostgresDatabaseIdentity Parse(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var value = new NpgsqlConnectionStringBuilder(connectionString);
        var hosts = value.Host.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length != 1)
            throw new InvalidOperationException("Atomic PostgreSQL transactions require one explicit database host.");
        if (string.IsNullOrWhiteSpace(value.Database))
            throw new InvalidOperationException("Atomic PostgreSQL transactions require one explicit database name.");
        return new(hosts[0].ToUpperInvariant(), value.Port, value.Database,
            value.Username ?? string.Empty);
    }

    public static void RequireSamePhysicalDatabase(IDbConnectionSettings settings, string firstName, string secondName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var first = settings[firstName] ?? throw new InvalidOperationException($"Connection setting '{firstName}' is required.");
        var second = settings[secondName] ?? throw new InvalidOperationException($"Connection setting '{secondName}' is required.");
        if (!first.ProviderName.Equals("System.Data.Postgres", StringComparison.Ordinal)
            || !second.ProviderName.Equals("System.Data.Postgres", StringComparison.Ordinal))
            throw new InvalidOperationException("Atomic persistence requires PostgreSQL for both named contexts.");
        if (Parse(first.ConnectionString) != Parse(second.ConnectionString))
            throw new InvalidOperationException("The enlisted contexts must identify the same physical PostgreSQL database and security identity.");
    }
}
