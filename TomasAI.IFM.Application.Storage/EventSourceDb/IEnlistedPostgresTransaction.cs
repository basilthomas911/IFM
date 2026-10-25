using Npgsql;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Request-scoped operations on an EventSource-owned PostgreSQL transaction.</summary>
public interface IEnlistedPostgresTransaction
{
    Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null);
    Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
        Func<System.Data.Common.DbDataReader, T> map, CancellationToken cancellationToken, PostgresQueryTimeout? timeout = null);
}
