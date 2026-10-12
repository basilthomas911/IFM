using System.Collections.Concurrent;
using System.Data;
using Npgsql;

namespace TomasAI.IFM.Framework.Storage.Postgres;

public class PostgresObjectDataRepositoryConnection : IObjectRepositoryConnection<NpgsqlConnection>
{
    static readonly ConcurrentDictionary<string, Lazy<NpgsqlDataSource>> DataSources =
        new(StringComparer.Ordinal);

    internal static int CachedDataSourceCount => DataSources.Count;

    public TConnection As<TConnection>(string connectionString) where TConnection : class, IDbConnection
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var connectionSettings = DatabaseCredentialResolver.GetPostgresConnectionSettings(connectionString);
        var normalizedConnectionString = connectionSettings.ConnectionString;
        var cacheKey = DatabaseCredentialResolver.GetCanonicalConnectionKey(connectionSettings);
        var dataSourceFactory = DataSources.GetOrAdd(
            cacheKey,
            _ => new Lazy<NpgsqlDataSource>(
                () => CreateDataSource(normalizedConnectionString),
                LazyThreadSafetyMode.ExecutionAndPublication));

        NpgsqlDataSource dataSource;
        try
        {
            dataSource = dataSourceFactory.Value;
        }
        catch
        {
            ((ICollection<KeyValuePair<string, Lazy<NpgsqlDataSource>>>)DataSources)
                .Remove(new KeyValuePair<string, Lazy<NpgsqlDataSource>>(cacheKey, dataSourceFactory));
            throw;
        }

        return dataSource.CreateConnection() as TConnection
            ?? throw new InvalidCastException(
                $"PostgreSQL connections cannot be converted to '{typeof(TConnection)}'.");
    }

    /// <summary>Resolves the host's environment credential without allowing credentials in shared configuration.</summary>
    /// <remarks>The result is for trusted provider construction only; never log or persist it.</remarks>
    public static string ResolveHostConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var credentials = DatabaseCredentialResolver.Resolve(DatabaseProvider.Postgres);
        return DatabaseCredentialResolver.AddPostgresCredentials(connectionString, credentials);
    }

    static NpgsqlDataSource CreateDataSource(string connectionString)
        => NpgsqlDataSource.Create(ResolveHostConnectionString(connectionString));
}
